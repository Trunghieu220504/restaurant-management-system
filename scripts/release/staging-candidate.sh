#!/bin/bash
# Operator-approved staging candidate. Preserves SQL data; never downgrades schema.
# Requires the checksum-pinned installer and root-owned release bundle.
set -Eeuo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
umask 077
# Preserve the operator error stream when a failing SQL function redirects its logs.
exec 8>&2
mode=${1:---plan}
if [ "$#" -gt 1 ] || { [ "$mode" != --plan ] && [ "$mode" != --apply ]; } ||
   [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then
    echo 'Root on restaurant-staging only: --plan or --apply.' >&2; exit 2
fi
release=$(dirname "$(readlink -f "$0")")
[[ "$release" =~ ^/opt/restaurant/releases/[0-9a-f]{40}$ ]] || exit 2
manifest="$release/release.json"
revision=$(jq -er .revision "$manifest")
[[ "$revision" =~ ^[0-9a-f]{40}$ ]] && [ "$release" = "/opt/restaurant/releases/$revision" ]
api_image=$(jq -er .apiImage "$manifest")
web_image=$(jq -er .webImage "$manifest")
[[ "$api_image" =~ ^sha256:[0-9a-f]{64}$ ]] && [[ "$web_image" =~ ^sha256:[0-9a-f]{64}$ ]]
exec 9>/run/lock/restaurant-staging-deploy.lock
flock -n 9 || { echo 'Another deployment is running.' >&2; exit 1; }
test ! -L "$release" && test "$(stat -c '%U' "$release")" = root
test -z "$(find "$release" -perm /022 -print -quit)"
(cd "$release" && sha256sum -c SHA256SUMS >/dev/null)
app=/home/deploy/restaurant-management-system
config=$release/compose
sql=restaurant-staging-sqlserver-1
api=restaurant-staging-api-1
web=restaurant-staging-web-1
nginx=restaurant-staging-nginx-1
for file in "$config" "$config/.env" "$config/compose.backend.yml" "$config/compose.backend.https.yml" \
    "$config/compose.frontend.yml" "$config/compose.backend.ghcr.yml" \
    "$config/compose.frontend.ghcr.yml" "$config/compose.week1-trusted-proxy.yml"; do
    test ! -L "$file" && test "$(stat -c '%U' "$file")" = root
    test -z "$(find "$file" -maxdepth 0 -perm /022 -print)"
done
for container in "$api" "$web" "$sql" "$nginx"; do
    test "$(docker inspect -f '{{.State.Running}}' "$container")" = true
done
old_api=$(docker inspect -f '{{.Image}}' "$api")
old_web=$(docker inspect -f '{{.Image}}' "$web")
sql_id=$(docker inspect -f '{{.Id}}' "$sql")
nginx_image=$(docker inspect -f '{{.Image}}' "$nginx")
database=$(docker inspect "$api" | jq -er '
  .[0].Config.Env[] | select(startswith("ConnectionStrings__Restaurant=")) | ltrimstr("ConnectionStrings__Restaurant=") |
  split(";") | map(select(test("^(Database|Initial Catalog)="; "i"))) |
  if length == 1 then .[0] | sub("^[^=]+="; "") else error("Ambiguous database") end')
[[ "$database" =~ ^[A-Za-z][A-Za-z0-9_]{0,100}$ ]]
network=$(docker inspect "$api" | jq -er '.[0].NetworkSettings.Networks | keys | if length == 1 then .[0] else error("Unexpected network count") end')
peer=$(docker inspect "$nginx" | jq -er --arg network "$network" '.[0].NetworkSettings.Networks[$network].IPAddress')
test "$(docker inspect "$api" | jq -er '.[0].Config.Env[] | select(startswith("ReverseProxy__KnownProxies__0=")) | split("=")[1]')" = "$peer"
compose=(docker compose --project-name restaurant-staging --project-directory "$app/infra" --env-file "$config/.env"
  -f "$config/compose.backend.yml" -f "$config/compose.backend.https.yml" -f "$config/compose.frontend.yml"
  -f "$config/compose.backend.ghcr.yml" -f "$config/compose.frontend.ghcr.yml"
  -f "$config/compose.week1-trusted-proxy.yml")
export GHCR_API_IMAGE="$api_image" GHCR_WEB_IMAGE="$web_image"
[[ "$peer" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]
# Move Nginx to reviewed root-owned config; resolve its actual IP after recreation.
"${compose[@]}" config --format json | jq -e '.services.nginx.networks.internal != null' >/dev/null
jq -n --arg config "$release/nginx.conf" --arg image "$nginx_image" '
 {services:{api:{environment:{Outbox__Enabled:"true",Outbox__PollSeconds:"1"}},
 nginx:{image:$image,volumes:[{type:"bind",source:$config,target:"/etc/nginx/conf.d/default.conf",read_only:true}]}}}' > "$release/runtime.override.json"
compose+=(-f "$release/runtime.override.json")
# Docker may assign a new bridge address; trust only the actual Nginx container.
sync_proxy() {
    local override=$1 actual
    actual=$(docker inspect "$nginx" | jq -er --arg network "$network" '.[0].NetworkSettings.Networks[$network].IPAddress') || return 1
    [[ "$actual" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || return 1
    jq --arg peer "$actual" '.services.api.environment.ReverseProxy__KnownProxies__0=$peer' "$override" > "$override.tmp" || return 1
    mv -f "$override.tmp" "$override" || return 1
    "${compose[@]}" -f "$override" up -d --no-deps --no-build --pull never api || return 1
}
"${compose[@]}" config --quiet
docker exec "$nginx" nginx -t >/dev/null 2>&1
docker run --rm --network "$network" --volumes-from "$nginx":ro   --mount "type=bind,src=$release/nginx.conf,dst=/etc/nginx/conf.d/default.conf,readonly"   "$nginx_image" nginx -t >/dev/null 2>&1
for image in "$api_image" "$web_image"; do
    test "$(docker image inspect -f '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")" = "$revision"
done
curl --fail --silent --show-error --connect-timeout 5 --max-time 15 https://staging.quanlynh.id.vn/health/ready >/dev/null
sql_run() {
    local target=$1
    # sqlcmd does not treat a UTF-8 BOM on stdin as file encoding metadata.
    # Strip only an initial BOM after the immutable file checksum check; retain all SQL text.
    sed '1s/^\xEF\xBB\xBF//' | docker exec -i "$sql" bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD";
      exec /opt/mssql-tools18/bin/sqlcmd -f i:65001 -C -I -b -r 1 -h -1 -W -s "|" -S localhost -U sa -d "$1"' _ "$target"
}
database_kb=$(sql_run master <<SQL | tr -d '[:space:]'
SET NOCOUNT ON;
SELECT SUM(CONVERT(bigint,size))*8 FROM sys.master_files WHERE database_id=DB_ID(N'$database');
SQL
)
[[ "$database_kb" =~ ^[0-9]+$ ]]
volume_source=$(docker inspect "$sql" | jq -er '.[0].Mounts[] | select(.Destination == "/var/opt/mssql") | .Source')
sql_free_kb=$(df -Pk "$volume_source" | awk 'NR == 2 {print $4}')
host_free_kb=$(df -Pk /var/backups | awk 'NR == 2 {print $4}')
test "$sql_free_kb" -gt "$((database_kb * 4 + 1048576))"
test "$host_free_kb" -gt "$((database_kb + 1048576))"
echo "Plan: deploy operator-approved staging candidate $revision using locally loaded immutable image IDs."
echo 'Backup CHECKSUM + VERIFYONLY; restore into a new scratch DB; apply all module migrations twice there; old and new API readiness on that copy.'
echo 'Then apply additive migrations to staging, replace API/Web and recreate Nginx and pin the API trusted proxy to its actual address, verify readiness/revisions and retain rollback state.'
echo 'SQL container ID and volume must stay unchanged; Nginx configuration is updated. No reset, bootstrap Admin or automatic schema downgrade.'
if [ "$mode" = --plan ]; then exit 0; fi
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
backup=/var/backups/restaurant-staging-release/$stamp
install -d -o root -g root -m 0700 "$backup"
printf 'api=%s\nweb=%s\n' "$old_api" "$old_web" > "$backup/previous-images"
docker exec "$nginx" cat /etc/nginx/conf.d/default.conf > "$backup/nginx.previous.conf"
old_outbox=$(docker inspect "$api" | jq -r '[.[0].Config.Env[] | select(startswith("Outbox__Enabled=")) | ltrimstr("Outbox__Enabled=")][0] // "false"')
jq -n --arg config "$backup/nginx.previous.conf" --arg image "$nginx_image" --arg outbox "$old_outbox" '
 {services:{api:{environment:{Outbox__Enabled:$outbox}},
 nginx:{image:$image,volumes:[{type:"bind",source:$config,target:"/etc/nginx/conf.d/default.conf",read_only:true}]}}}' > "$backup/rollback.override.json"
restore_db=RestaurantReleaseVerify_$(date -u +%Y%m%d%H%M%S)_$$
[[ "$restore_db" =~ ^RestaurantReleaseVerify_[0-9]+_[0-9]+$ ]]
probe=restaurant-release-probe-$stamp
restore_started=false
images_switched=false
stage='backup-and-verify'
cleanup() {
    local status=$?
    local cleanup_status=0
    trap - EXIT
    set +e
    if docker inspect "$probe" >/dev/null 2>&1; then
        docker rm -f "$probe" > "$backup/probe-cleanup.log" 2>&1 || cleanup_status=1
    fi
    if [ "$restore_started" = true ]; then
        sql_run master <<SQL > "$backup/restore-cleanup.log" 2>&1 || cleanup_status=1
IF DB_ID(N'$restore_db') IS NOT NULL BEGIN
  ALTER DATABASE [$restore_db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [$restore_db];
END;
SQL
    fi
    if [ "$cleanup_status" -ne 0 ]; then
        echo "Scratch cleanup failed; inspect private evidence: $backup" >&8
        if [ "$status" -eq 0 ]; then status=2; fi
    fi
    exit "$status"
}
trap cleanup EXIT
rollback_on_error() {
    local status=$?
    trap - ERR
    set +e
    if [ "$images_switched" = true ]; then
        export GHCR_API_IMAGE="$old_api" GHCR_WEB_IMAGE="$old_web"
        if "${compose[@]}" -f "$backup/rollback.override.json" up -d --no-deps --no-build --pull never --force-recreate api web nginx > "$backup/rollback.log" 2>&1 &&
           sync_proxy "$backup/rollback.override.json" >> "$backup/rollback.log" 2>&1 &&
           docker exec "$nginx" nginx -t >> "$backup/rollback.log" 2>&1 &&
           docker exec "$nginx" nginx -s reload >> "$backup/rollback.log" 2>&1; then
            for attempt in $(seq 1 30); do
                if curl --fail --silent --connect-timeout 3 --max-time 5 https://staging.quanlynh.id.vn/health/ready >/dev/null; then
                    echo "Deployment failed during $stage; previous API/Web images and Nginx configuration restored; readiness passed. Schema retained." >&8
                    exit "$status"
                fi
                sleep 2
            done
        fi
        echo "Deployment failed during $stage and rollback readiness failed. Inspect private logs: $backup" >&8
    else
        echo "Stopped during $stage before switching API/Web. Inspect private logs: $backup" >&8
    fi
    exit "$status"
}
trap rollback_on_error ERR
backup_file=/var/opt/mssql/data/restaurant-release-$stamp.bak
sql_run "$database" <<SQL > "$backup/backup.log" 2>&1
BACKUP DATABASE [$database] TO DISK = N'$backup_file' WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = N'$backup_file' WITH CHECKSUM;
SQL
docker cp "$sql:$backup_file" "$backup/database.bak" >/dev/null 2>&1
chmod 0600 "$backup/database.bak"
sha256sum "$backup/database.bak" > "$backup/database.sha256"
stage='read-backup-filelist'
sql_run master <<SQL > "$backup/filelist" 2>&1
SET NOCOUNT ON;
IF DB_ID(N'$restore_db') IS NOT NULL THROW 51000, 'Scratch database collision', 1;
RESTORE FILELISTONLY FROM DISK = N'$backup_file';
SQL
printf "RESTORE DATABASE [%s] FROM DISK = N'%s' WITH CHECKSUM, RECOVERY" "$restore_db" "$backup_file" > "$backup/restore.sql"
files=0
while IFS='|' read -r logical _physical type _rest; do
    [ "$type" = D ] || [ "$type" = L ] || continue
    [[ "$logical" =~ ^[A-Za-z0-9_]+$ ]]
    files=$((files + 1))
    printf ", MOVE N'%s' TO N'/var/opt/mssql/data/%s_%s.%s'" "$logical" "$restore_db" "$files" "$type" >> "$backup/restore.sql"
done < "$backup/filelist"
test "$files" -ge 2
printf ';\n' >> "$backup/restore.sql"
restore_started=true
stage='restore-isolated-copy'
sql_run master < "$backup/restore.sql" > "$backup/restore.log" 2>&1
for attempt in 1 2; do
    while IFS= read -r file; do
        [[ "$file" =~ ^(Identity|SalesDay|Catalog|Seating|Sales|Billing|Inventory|Fulfillment|Reporting|Audit)\.sql$ ]] || exit 2
        stage="verify-$file-$attempt"
        sql_run "$restore_db" < "$release/migrations/$file" > "$backup/verify-$file-$attempt.log" 2>&1
    done < "$release/migrations/order.txt"
done
stage='check-isolated-copy'
sql_run "$restore_db" <<SQL > "$backup/check-copy.log" 2>&1
IF OBJECT_ID(N'audit.NhatKy') IS NULL OR OBJECT_ID(N'fulfillment.PhieuCheBien') IS NULL OR OBJECT_ID(N'sales.DonHang') IS NULL THROW 51000, 'Migration objects missing', 1;
DBCC CHECKDB ([$restore_db]) WITH NO_INFOMSGS;
SQL
stage='api-probes-on-migrated-copy'
docker inspect "$api" | jq -r --arg db "$restore_db" '
 .[0].Config.Env[] | select(startswith("Outbox__Enabled=") | not) |
 if startswith("ConnectionStrings__Restaurant=") then
 sub("(?i)(Database|Initial Catalog)=[^;]+"; "Database=" + $db) else . end' > "$backup/probe.env"
printf 'Outbox__Enabled=false\n' >> "$backup/probe.env"
for image in "$old_api" "$api_image"; do
    docker run -d --name "$probe" --network "$network" --env-file "$backup/probe.env" "$image" >/dev/null
    ready=false
    for attempt in $(seq 1 30); do
        if docker exec "$probe" bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /health/ready HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3; head -n 1 <&3 | grep -q " 200 "' >/dev/null 2>&1; then ready=true; break; fi
        sleep 2
    done
    test "$ready" = true
    docker rm -f "$probe" >/dev/null
done
# Quiesce application writers before touching live schema. Any later error restarts old images.
images_switched=true
stage='stop-application-writers'
"${compose[@]}" stop web api > "$backup/stop.log" 2>&1
while IFS= read -r file; do
    [[ "$file" =~ ^(Identity|SalesDay|Catalog|Seating|Sales|Billing|Inventory|Fulfillment|Reporting|Audit)\.sql$ ]] || exit 2
    stage="apply-$file"
    sql_run "$database" < "$release/migrations/$file" > "$backup/apply-$file.log" 2>&1
done < "$release/migrations/order.txt"
stage='replace-api-web'
images_switched=true
"${compose[@]}" up -d --no-deps --no-build --pull never --force-recreate api web nginx > "$backup/deploy.log" 2>&1
stage='update-trusted-proxy'
sync_proxy "$release/runtime.override.json" >> "$backup/deploy.log" 2>&1
stage='reload-nginx-and-verify-public-readiness'
docker exec "$nginx" nginx -t > "$backup/nginx.log" 2>&1
docker exec "$nginx" nginx -s reload >> "$backup/nginx.log" 2>&1
ready=false
for attempt in $(seq 1 30); do
    if curl --fail --silent --connect-timeout 3 --max-time 5 https://staging.quanlynh.id.vn/health/ready >/dev/null; then ready=true; break; fi
    sleep 2
done
test "$ready" = true
curl --fail --silent --show-error --max-time 15 https://staging.quanlynh.id.vn/ >/dev/null
for container in "$api" "$web"; do
    test "$(docker inspect -f '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$container")" = "$revision"
done
test "$(docker inspect -f '{{.Id}}' "$sql")" = "$sql_id"
test "$(docker inspect "$nginx" | jq -er --arg network "$network" '.[0].NetworkSettings.Networks[$network].IPAddress')" = "$(docker inspect "$api" | jq -er '.[0].Config.Env[] | select(startswith("ReverseProxy__KnownProxies__0=")) | split("=")[1]')"
stage='record-release-state'
install -d -o root -g root -m 0700 /var/lib/restaurant-staging
printf 'REVISION=%s\nGHCR_API_IMAGE=%s\nGHCR_WEB_IMAGE=%s\nBACKUP=%s\n' "$revision" "$api_image" "$web_image" "$backup" > /var/lib/restaurant-staging/release.env.new
mv -f -- /var/lib/restaurant-staging/release.env.new /var/lib/restaurant-staging/release.env
trap - ERR
echo "Deployed $revision; local backup/isolated restore/migration-twice/old-new readiness passed. Offsite backup and business UAT are separate gates."
echo "SQL container unchanged; API trusts only the actual Nginx proxy IP. Private evidence: $backup"
