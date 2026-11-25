#!/usr/bin/env bash
set -euo pipefail
# shellcheck source=common.sh
source /usr/local/lib/restaurant/common.sh
[ "$#" -eq 0 ] || exit 2
exec 9>/run/lock/restaurant-backup.lock
flock -n 9 || exit 1
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
name="$DATABASE-$stamp.bak"
container_path="/var/opt/mssql/backup/$name"
local_path="$BACKUP_DIRECTORY/$name"
cleanup() {
    rm -f -- "$local_path"
    docker exec "$SQL_CONTAINER" rm -f -- "$container_path" >/dev/null 2>&1 || true
}
trap cleanup EXIT
docker exec "$SQL_CONTAINER" mkdir -p /var/opt/mssql/backup
printf "BACKUP DATABASE [%s] TO DISK=N'%s' WITH COPY_ONLY, CHECKSUM, COMPRESSION;\nRESTORE VERIFYONLY FROM DISK=N'%s' WITH CHECKSUM;\nGO\n" "$DATABASE" "$container_path" "$container_path" | sql_run >/dev/null
docker cp "$SQL_CONTAINER:$container_path" "$local_path"
chmod 0600 "$local_path"
age -r "$AGE_RECIPIENT" -o "$local_path.age" "$local_path"
(cd "$BACKUP_DIRECTORY" && sha256sum "$name.age" > "$name.age.sha256")
rclone --config "$RCLONE_CONFIG" copyto "$local_path.age" "$BACKUP_REMOTE/$name.age"
rclone --config "$RCLONE_CONFIG" copyto "$local_path.age.sha256" "$BACKUP_REMOTE/$name.age.sha256"
# Read the object back; an upload exit code alone is insufficient evidence.
rclone --config "$RCLONE_CONFIG" cat "$BACKUP_REMOTE/$name.age" | sha256sum | cut -d ' ' -f 1 > "$local_path.remote-sha256"
expected=$(cut -d ' ' -f 1 "$local_path.age.sha256")
[ "$(cat "$local_path.remote-sha256")" = "$expected" ]
rm -f -- "$local_path.remote-sha256"
printf '{"status":"PASS","utc":"%s","object":"%s","sha256":"%s"}\n' "$(date -u +%FT%TZ)" "$name.age" "$expected" > "$BACKUP_DIRECTORY/latest.json"
# Offsite expiry is a bucket lifecycle policy, never an unrestricted remote delete.
find "$BACKUP_DIRECTORY" -maxdepth 1 -type f -name "$DATABASE-*.bak.age*" -mtime +7 -delete
printf 'Backup and offsite checksum PASS: %s\n' "$name.age"
