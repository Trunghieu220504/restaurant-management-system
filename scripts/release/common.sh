#!/usr/bin/env bash
# Installed root-owned at /usr/local/lib/restaurant/common.sh; never sourced from checkout by sudo.
set -euo pipefail
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
umask 077
[ "$(id -u)" -eq 0 ] || { echo 'Root operator required' >&2; exit 2; }
config=/etc/restaurant/operations.env
[ -f "$config" ] && [ ! -L "$config" ] && [ "$(stat -c %u "$config")" = 0 ] && [ "$(stat -c %a "$config")" = 600 ] || exit 2
# shellcheck source=/dev/null
source "$config"
: "${SQL_CONTAINER:?}" "${DATABASE:?}" "${BACKUP_DIRECTORY:?}" "${RCLONE_CONFIG:?}" "${BACKUP_REMOTE:?}" "${AGE_RECIPIENT:?}"
[[ "$DATABASE" =~ ^[A-Za-z][A-Za-z0-9_]{0,63}$ ]] || exit 2
[[ "$SQL_CONTAINER" =~ ^[a-zA-Z0-9][a-zA-Z0-9_.-]+$ ]] || exit 2
[[ "$BACKUP_DIRECTORY" = /var/backups/restaurant/* ]] || exit 2
[[ "$BACKUP_REMOTE" =~ ^[A-Za-z0-9_-]+:[A-Za-z0-9_-]+/[A-Za-z0-9_/-]+$ ]] || exit 2
install -d -m 0700 "$BACKUP_DIRECTORY"
sql_run() {
    # Input must be generated only from validated identifiers. sqlcmd errors propagate.
    docker exec -i "$SQL_CONTAINER" bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -C -b -r 1 -S localhost -U sa -d master -h -1 -W'
}
