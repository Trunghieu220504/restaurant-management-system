#!/usr/bin/env bash
# Root-owned deployment helper. Config and compose must also be root-owned.
# No automatic database downgrade or restore: old/new API compatibility is a release prerequisite.
set -Eeuo pipefail
# shellcheck source=common.sh
source /usr/local/lib/restaurant/common.sh
[ "$#" -eq 1 ] && [[ "$1" =~ ^[0-9a-f]{40}$ ]] || exit 2
revision=$1
: "${COMPOSE_DIRECTORY:?}" "${STAGING_ORIGIN:?}" "${MIGRATION_COMPATIBILITY_DIRECTORY:?}"
[ "$COMPOSE_DIRECTORY" = /etc/restaurant/staging ] || exit 2
[ "$STAGING_ORIGIN" = https://staging.quanlynh.id.vn ] || exit 2
# An operator records old-version compatibility against a restored database for this exact SHA.
# A mere Jenkins green build cannot prove production-schema compatibility.
compatibility="$MIGRATION_COMPATIBILITY_DIRECTORY/$revision.json"
python3 - "$compatibility" "$revision" <<'PY'
import json, os, stat, sys
path, revision = sys.argv[1:]
s = os.lstat(path)
assert stat.S_ISREG(s.st_mode) and s.st_uid == 0 and not (s.st_mode & 0o022)
p = json.load(open(path))
assert p['revision'] == revision and p['previousApiOnMigratedDatabase'] == 'PASS'
assert p['migrationTwice'] == 'PASS' and p['reviewer'] and p['evidence']
PY
exec 8>/run/lock/restaurant-deploy.lock
flock -n 8 || exit 1
install -d -m 0700 /var/lib/restaurant/releases
release="/var/lib/restaurant/releases/$revision"
install -d -m 0700 "$release"
compose() { docker compose --project-name restaurant-staging --project-directory "$COMPOSE_DIRECTORY" --env-file "$COMPOSE_DIRECTORY/runtime.env" -f "$COMPOSE_DIRECTORY/compose.yml" -f "$COMPOSE_DIRECTORY/images.yml" "$@"; }
# Save old IMMUTABLE digests before any service changes.
api_id=$(compose ps -q api)
web_id=$(compose ps -q web)
[ -n "$api_id" ] && [ -n "$web_id" ]
old_api=$(docker inspect -f '{{.Config.Image}}' "$api_id")
old_web=$(docker inspect -f '{{.Config.Image}}' "$web_id")
[[ "$old_api" =~ @sha256:[0-9a-f]{64}$ ]] && [[ "$old_web" =~ @sha256:[0-9a-f]{64}$ ]] || exit 2
registry=ghcr.io/trunghieu220504
for component in api web migrations; do
    image="$registry/restaurant-$component:$revision"
    docker pull "$image"
    [ "$(docker image inspect -f '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")" = "$revision" ]
    docker image inspect -f '{{index .RepoDigests 0}}' "$image" > "$release/$component.digest"
done
api=$(cat "$release/api.digest")
web=$(cat "$release/web.digest")
migrations=$(cat "$release/migrations.digest")
for image in "$api" "$web" "$migrations"; do [[ "$image" =~ ^ghcr.io/trunghieu220504/restaurant-(api|web|migrations)@sha256:[0-9a-f]{64}$ ]] || exit 2; done
/usr/local/lib/restaurant/backup.sh
container=$(docker create "$migrations" /unused)
cleanup() { docker rm -v "$container" >/dev/null 2>&1 || true; }
trap cleanup EXIT
docker cp "$container:/migrations" "$release/"
(cd "$release/migrations" && sha256sum -c SHA256SUMS)
# Stop both writers before migrations. Rollback restarts previous images on compatible schema.
write_images() {
    printf 'services:\n  api:\n    image: %s\n  web:\n    image: %s\n' "$1" "$2" > "$COMPOSE_DIRECTORY/images.yml.tmp"
    mv "$COMPOSE_DIRECTORY/images.yml.tmp" "$COMPOSE_DIRECTORY/images.yml"
}
rollback() {
    local status=$?
    trap - ERR
    echo 'Deployment failed; restoring previous API/web image digests.' >&2
    write_images "$old_api" "$old_web"
    if compose up -d --no-build --no-deps api web && compose exec -T nginx nginx -s reload && python3 /usr/local/lib/restaurant/smoke.py "$STAGING_ORIGIN" --output "$release/rollback.json"; then
        echo 'Previous images restored; original release remains FAILED.' >&2
    else
        echo 'ROLLBACK FAILED: operator intervention required.' >&2
    fi
    exit "$status"
}
trap rollback ERR
compose stop web api
while IFS= read -r file; do
    [[ "$file" =~ ^(Identity|SalesDay|Catalog|Seating|Sales|Billing|Inventory|Fulfillment|Reporting|Audit)\.sql$ ]] || false
    # Fixed SQL destination; UTF-8 without BOM from export-migrations.sh.
    if { printf 'USE [%s];\nGO\n' "$DATABASE"; cat "$release/migrations/$file"; } | sql_run; then :; else false; fi
done < "$release/migrations/order.txt"
write_images "$api" "$web"
compose up -d --no-build --no-deps api web
compose exec -T nginx nginx -s reload
ready=false
for ((attempt=0; attempt<30; attempt++)); do
    if python3 /usr/local/lib/restaurant/smoke.py "$STAGING_ORIGIN" --output "$release/smoke.json" >/dev/null; then ready=true; break; fi
    sleep 3
done
[ "$ready" = true ]
trap - ERR
printf '{"status":"PASS","revision":"%s","utc":"%s"}\n' "$revision" "$(date -u +%FT%TZ)" > "$release/deploy.json"
echo "Deployment PASS: $revision"
