#!/bin/bash
# Existing staging only. No image pull/build, SQL migration, network or firewall changes.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
mode=${1:---plan}
if [ "$#" -gt 1 ] || { [ "$mode" != --plan ] && [ "$mode" != --apply ]; } ||
   [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then
    echo 'Run as root on restaurant-staging: --plan (default) or --apply.' >&2
    exit 2
fi
exec 9>/run/lock/restaurant-week1-proxy.lock
flock -n 9 || { echo 'Another proxy repair is running.' >&2; exit 1; }
work=/home/deploy/restaurant-management-system/infra
release=/home/deploy/hai-r8-release-7de77d4cbd15/infra
api=restaurant-staging-api-1
nginx=restaurant-staging-nginx-1
web=restaurant-staging-web-1
sql=restaurant-staging-sqlserver-1
network=restaurant-staging_internal
target=$work/compose.week1-trusted-proxy.yml
base=(docker compose --project-name restaurant-staging --project-directory "$work" --env-file "$work/.env"
    -f "$work/compose.backend.yml" -f "$work/compose.backend.https.yml" -f "$work/compose.frontend.yml"
    -f "$release/compose.backend.ghcr.yml" -f "$release/compose.frontend.ghcr.yml")
expected_files="$work/compose.backend.yml,$work/compose.backend.https.yml,$work/compose.frontend.yml,$release/compose.backend.ghcr.yml,$release/compose.frontend.ghcr.yml"
api_json=$(docker inspect "$api")
nginx_json=$(docker inspect "$nginx")
proxy_ip=$(jq -er --arg network "$network" '.[0].NetworkSettings.Networks[$network].IPAddress | select(test("^[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+$"))' <<< "$nginx_json")
jq -e --arg network "$network" '.[0].NetworkSettings.Networks | (keys == [$network])' <<< "$api_json" >/dev/null
jq -e --arg network "$network" '.[0].NetworkSettings.Networks | (keys == [$network])' <<< "$nginx_json" >/dev/null
known=$(jq -r '.[0].Config.Env[] | select(startswith("ReverseProxy__KnownProxies__"))' <<< "$api_json")
if [ "$known" = "ReverseProxy__KnownProxies__0=$proxy_ip" ]; then
    echo "Runtime already trusts only Nginx $proxy_ip. No change made."
    exit 0
fi
if [ -n "$known" ]; then
    echo 'Unexpected existing trusted proxies; review manually. Nothing changed.' >&2
    exit 1
fi
actual_files=$(jq -r '.[0].Config.Labels["com.docker.compose.project.config_files"]' <<< "$api_json")
if [ "$actual_files" != "$expected_files" ] || [ -e "$target" ]; then
    echo 'Compose provenance/override differs from reviewed baseline. Nothing changed.' >&2
    exit 1
fi
export GHCR_API_IMAGE
export GHCR_WEB_IMAGE
GHCR_API_IMAGE=$(jq -r '.[0].Config.Image' <<< "$api_json")
GHCR_WEB_IMAGE=$(docker inspect --format '{{.Config.Image}}' "$web")
expected_api=ghcr.io/trunghieu220504/restaurant-api:7de77d4cbd15deb474bab146ae6f14dd05937dbe
expected_web=ghcr.io/trunghieu220504/restaurant-web:7de77d4cbd15deb474bab146ae6f14dd05937dbe
if [ "$GHCR_API_IMAGE" != "$expected_api" ] || [ "$GHCR_WEB_IMAGE" != "$expected_web" ]; then
    echo 'Live release changed; review again before applying. Nothing changed.' >&2
    exit 1
fi
api_image_id=$(jq -r '.[0].Image' <<< "$api_json")
test "$(docker image inspect --format '{{.Id}}' "$GHCR_API_IMAGE")" = "$api_image_id"
baseline_hash=$("${base[@]}" config --hash api | awk '$1 == "api" {print $2}')
live_hash=$(jq -r '.[0].Config.Labels["com.docker.compose.config-hash"]' <<< "$api_json")
if [ -z "$baseline_hash" ] || [ "$baseline_hash" != "$live_hash" ]; then
    echo 'Current Compose inputs differ from the live API configuration hash. Nothing changed.' >&2
    exit 1
fi
baseline=$("${base[@]}" config --format json)
jq -e '.services.api | ((.ports // [] | length) == 0) and ((.volumes // [] | length) == 0)' <<< "$baseline" >/dev/null
jq -e '.[0] | ((.Mounts | length) == 0) and ((.HostConfig.PortBindings // {} | length) == 0)' <<< "$api_json" >/dev/null
docker exec "$nginx" nginx -t
sql_before=$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$sql")
nginx_before=$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$nginx")
web_before=$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$web")
printf 'Plan: add exactly ReverseProxy__KnownProxies__0=%s; recreate only API using the same local image.\n' "$proxy_ip"
echo 'No SQL/Web/Nginx container recreation, image pull/build or network change. Nginx reload refreshes API DNS.'
if [ "$mode" = --plan ]; then exit 0; fi
backup=/var/backups/restaurant-week1-proxy/$(date -u +%Y%m%dT%H%M%SZ)-$$
install -d -o root -g root -m 0700 "$backup"
printf '%s\n' "$api_json" > "$backup/api.inspect.json"
printf '%s\n' "$baseline" > "$backup/baseline.compose.json"
cp -a -- "$work/.env" "$backup/runtime.env"
for file in "$work/compose.backend.yml" "$work/compose.backend.https.yml" "$work/compose.frontend.yml" "$release/compose.backend.ghcr.yml" "$release/compose.frontend.ghcr.yml"; do
    cp -a --parents -- "$file" "$backup/"
done
temporary=$(mktemp "$work/.week1-proxy.XXXXXX")
cleanup() { rm -f -- "$temporary"; }
trap cleanup EXIT
cat > "$temporary" <<CONFIG
# Keep in the Compose file list for future API deployments.
# Revalidate this exact peer address whenever Nginx is recreated or moved.
services:
  api:
    environment:
      ReverseProxy__KnownProxies__0: "$proxy_ip"
CONFIG
proposed=("${base[@]}" -f "$temporary")
"${proposed[@]}" config --format json > "$backup/proposed.compose.json"
jq -e -n --slurpfile before "$backup/baseline.compose.json" --slurpfile after "$backup/proposed.compose.json" \
    '($after[0] | del(.services.api.environment.ReverseProxy__KnownProxies__0)) == $before[0]' >/dev/null
rollback() {
    trap - ERR
    set +e
    rm -f -- "$target"
    "${base[@]}" up -d --no-deps --no-build --pull never api > "$backup/rollback.log" 2>&1
    rollback_status=$?
    docker exec "$nginx" nginx -t >> "$backup/rollback.log" 2>&1 && docker exec "$nginx" nginx -s reload >> "$backup/rollback.log" 2>&1
    echo "Repair failed. Baseline API restore exit=$rollback_status. Private backup/log: $backup" >&2
    exit 1
}
trap rollback ERR
install -o root -g root -m 0644 "$temporary" "$target"
test "$("${base[@]}" config --hash api | awk '$1 == "api" {print $2}')" = "$baseline_hash"
compose=("${base[@]}" -f "$target")
"${compose[@]}" up -d --no-deps --no-build --pull never api > "$backup/apply.log" 2>&1
docker exec "$nginx" nginx -t
docker exec "$nginx" nginx -s reload
healthy=false
for attempt in $(seq 1 20); do
    if curl --fail --silent --show-error --max-time 5 -o /dev/null https://staging.quanlynh.id.vn/health/ready 2>/dev/null; then healthy=true; break; fi
    sleep 2
done
test "$healthy" = true
test "$(docker inspect --format '{{.Image}}' "$api")" = "$api_image_id"
test "$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$sql")" = "$sql_before"
test "$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$nginx")" = "$nginx_before"
test "$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$web")" = "$web_before"
applied=$(docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$api" | grep '^ReverseProxy__KnownProxies__')
test "$applied" = "ReverseProxy__KnownProxies__0=$proxy_ip"
trap - ERR
printf 'Applied exact trusted peer; ready=200, image unchanged, SQL/Nginx/Web container start identities unchanged.\nOverride: %s\nPrivate backup: %s\n' "$target" "$backup"
