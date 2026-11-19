#!/bin/bash
# Read only the allowlisted logs from this observed failed deployment; no repair or privilege grant.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then
    echo 'Root on restaurant-staging only; no arguments.' >&2
    exit 2
fi
evidence=/var/backups/restaurant-staging-release/20261119T051605Z-220913
test -d "$evidence" && test ! -L "$evidence"
test "$(stat -c '%U' "$evidence")" = root
echo 'Read-only diagnosis of failed 95ab29b deployment (20261119T051605Z-220913).'
echo 'Only bounded deployment logs are shown; environment, credentials and SQL payload files are excluded.'
for name in backup.log pull-api.log pull-web.log restore.log \
    verify-seating-1.log verify-sales-1.log verify-seating-2.log verify-sales-2.log \
    check-copy.log probe-cleanup.log restore-cleanup.log apply-seating.log apply-sales.log \
    deploy.log nginx.log rollback.log; do
    file=$evidence/$name
    if [ ! -f "$file" ]; then continue; fi
    test ! -L "$file" && test "$(stat -c '%U' "$file")" = root
    printf '\nLOG %s (last 12 non-secret lines)\n' "$name"
    awk 'tolower($0) !~ /password|pwd=|bearer|access.?token|refresh.?token|connectionstrings|private.key|secret|cookie|authorization/' "$file" | tail -n 12
done
echo
for container in restaurant-staging-api-1 restaurant-staging-web-1; do
    docker inspect -f 'name={{.Name}} state={{.State.Status}} revision={{index .Config.Labels "org.opencontainers.image.revision"}}' "$container"
done
echo 'Remaining scratch database count:'
docker exec -i restaurant-staging-sqlserver-1 bash -c '
  export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
  exec /opt/mssql-tools18/bin/sqlcmd -C -b -r 1 -h -1 -W -S localhost -U sa -d master' <<'SQL'
SET NOCOUNT ON;
SELECT COUNT(*) FROM sys.databases WHERE name LIKE N'RestaurantReleaseVerify[_]%';
SQL
curl --fail --silent --show-error --max-time 15 https://staging.quanlynh.id.vn/health/ready
echo
echo 'Read-only diagnosis complete. No deployment, database write or account change was performed.'
