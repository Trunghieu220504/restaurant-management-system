#!/bin/bash
# One approved develop release only. Never rebuild, reset data, recreate SQL/Nginx or downgrade schema.
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
revision=95ab29baa09949f88dd09d106de0834108e97670
release=/opt/restaurant/releases/95ab29b
api_image=ghcr.io/trunghieu220504/restaurant-api@sha256:eb136dfcb81fbbaa23bafbc524e0bb7d776bed3eef260e6b64fb8f80915376b5
web_image=ghcr.io/trunghieu220504/restaurant-web@sha256:1f61ddcf33491a616c288eff1fabfaf15102e89701cc4271977def3eada3dd44
exec 9>/run/lock/restaurant-staging-deploy.lock
flock -n 9 || { echo 'Another deployment is running.' >&2; exit 1; }
for file in "$release" "$release/seating.sql" "$release/sales.sql"; do
    test ! -L "$file" && test "$(stat -c '%U' "$file")" = root
    test -z "$(find "$file" -maxdepth 0 -perm /022 -print)"
done
echo "574e35a3dbbad08afff2e76d0ac9d8ee85a9ec2b8400df06f3350b27817c79a2  $release/seating.sql" | sha256sum -c - >/dev/null
echo "458b581c151c6eafb96c92513b5b29b9d6b37f80281906b8ff1bfef2585068af  $release/sales.sql" | sha256sum -c - >/dev/null
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
nginx_id=$(docker inspect -f '{{.Id}}' "$nginx")
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
"${compose[@]}" config --quiet
docker exec "$nginx" nginx -t >/dev/null 2>&1
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
echo "Plan: deploy merged develop $revision using fixed public image digests."
echo 'Backup CHECKSUM + VERIFYONLY; restore into a new scratch DB; apply Seating/Sales twice there; API readiness on that copy.'
echo 'Then apply additive migrations to staging, replace API/Web only, reload Nginx, verify readiness/revisions and retain rollback state.'
echo 'SQL/Nginx container IDs must stay unchanged. No reset, bootstrap Admin or automatic schema downgrade.'
if [ "$mode" = --plan ]; then exit 0; fi
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
backup=/var/backups/restaurant-staging-release/$stamp
install -d -o root -g root -m 0700 "$backup"
printf 'api=%s\nweb=%s\n' "$old_api" "$old_web" > "$backup/previous-images"
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
        if "${compose[@]}" up -d --no-deps --no-build --pull never api web > "$backup/rollback.log" 2>&1 &&
           docker exec "$nginx" nginx -t >> "$backup/rollback.log" 2>&1 &&
           docker exec "$nginx" nginx -s reload >> "$backup/rollback.log" 2>&1; then
            for attempt in $(seq 1 30); do
                if curl --fail --silent --connect-timeout 3 --max-time 5 https://staging.quanlynh.id.vn/health/ready >/dev/null; then
                    echo "Deployment failed during $stage; previous API/Web images restored and readiness passed. Schema retained." >&8
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
stage='pull-and-verify-images'
docker pull "$api_image" > "$backup/pull-api.log" 2>&1
docker pull "$web_image" > "$backup/pull-web.log" 2>&1
for image in "$api_image" "$web_image"; do
    test "$(docker image inspect -f '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")" = "$revision"
done
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
    stage="verify-seating-$attempt"
    sql_run "$restore_db" < "$release/seating.sql" > "$backup/verify-seating-$attempt.log" 2>&1
    stage="verify-sales-$attempt"
    sql_run "$restore_db" < "$release/sales.sql" > "$backup/verify-sales-$attempt.log" 2>&1
done
stage='check-isolated-copy'
sql_run "$restore_db" <<SQL > "$backup/check-copy.log" 2>&1
IF OBJECT_ID(N'seating.YeuCauDaXuLy') IS NULL OR OBJECT_ID(N'sales.DonHang') IS NULL THROW 51000, 'Migration objects missing', 1;
DBCC CHECKDB ([$restore_db]) WITH NO_INFOMSGS;
SQL
stage='api-probe-on-copy'
docker inspect "$api" | jq -r --arg db "$restore_db" '
  .[0].Config.Env[] | if startswith("ConnectionStrings__Restaurant=") then
  sub("(?i)(Database|Initial Catalog)=[^;]+"; "Database=" + $db) else . end' > "$backup/probe.env"
docker run -d --name "$probe" --network "$network" --env-file "$backup/probe.env" "$api_image" >/dev/null
ready=false
for attempt in $(seq 1 30); do
    if docker exec "$probe" bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /health/ready HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3; head -n 1 <&3 | grep -q " 200 "' >/dev/null 2>&1; then ready=true; break; fi
    sleep 2
done
test "$ready" = true
docker rm -f "$probe" >/dev/null
for file in seating sales; do
    stage="apply-$file"
    sql_run "$database" < "$release/$file.sql" > "$backup/apply-$file.log" 2>&1
done
stage='replace-api-web'
images_switched=true
"${compose[@]}" up -d --no-deps --no-build --pull never api web > "$backup/deploy.log" 2>&1
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
test "$(docker inspect -f '{{.Id}}' "$nginx")" = "$nginx_id"
stage='record-release-state'
install -d -o root -g root -m 0700 /var/lib/restaurant-staging
printf 'REVISION=%s\nGHCR_API_IMAGE=%s\nGHCR_WEB_IMAGE=%s\nBACKUP=%s\n' "$revision" "$api_image" "$web_image" "$backup" > /var/lib/restaurant-staging/release.env.new
mv -f -- /var/lib/restaurant-staging/release.env.new /var/lib/restaurant-staging/release.env
trap - ERR
echo "Deployed $revision; backup/isolated restore/migration-twice/API probe/readiness passed."
echo "SQL/Nginx container IDs unchanged. Private evidence: $backup"
