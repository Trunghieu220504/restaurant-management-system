#!/bin/bash
# Replace only the fixed 95ab29b deploy helper after a checked, read-only plan.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
umask 077
if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then exit 2; fi
source_file=/home/deploy/restaurant-release-95ab29b/restaurant-deploy-95ab29b.sh
target=/opt/restaurant/releases/95ab29b/restaurant-deploy-95ab29b.sh
expected=3ba0f586fd0c28b34d5c8af1dc0d4fd18b60bc916d0bfe0cb6b24403b40cb94b
test -f "$source_file" && test ! -L "$source_file"
for directory in /opt/restaurant /opt/restaurant/releases /opt/restaurant/releases/95ab29b; do
    test -d "$directory" && test ! -L "$directory"
    test "$(stat -c '%U' "$directory")" = root
    test -z "$(find "$directory" -maxdepth 0 -perm /022 -print)"
done
test -f "$target" && test ! -L "$target" && test "$(stat -c '%U' "$target")" = root
backup=$(mktemp -d /var/backups/restaurant-staging-sql-fix.XXXXXXXX)
install -o root -g root -m 0700 "$source_file" "$backup/candidate.sh"
echo "$expected  $backup/candidate.sh" | sha256sum -c - >/dev/null
bash -n "$backup/candidate.sh"
"$backup/candidate.sh" --plan
exec 9>/run/lock/restaurant-staging-deploy.lock
flock -n 9 || { echo 'A deployment started; helper unchanged.' >&2; exit 1; }
previous=$(sha256sum "$target" | cut -d ' ' -f 1)
case "$previous" in
    8bc0f7e6f6a335911ef042809640fe04e95a24be6a6cb5d47546af60045e76b3|"$expected") ;;
    *) echo 'Unexpected installed helper; stop without replacing it.' >&2; exit 1 ;;
esac
install -o root -g root -m 0700 "$target" "$backup/previous.sh"
if install -o root -g root -m 0700 "$backup/candidate.sh" "$target" &&
   echo "$expected  $target" | sha256sum -c - >/dev/null; then
    echo 'Installed fixed UTF-8/BOM transport and phase error reporting; plan passed.'
    echo "Previous helper backup: $backup"
    echo 'No deployment, migration, account change, Compose/environment overwrite or sudo-rule change ran.'
else
    install -o root -g root -m 0700 "$backup/previous.sh" "$target"
    echo 'Replacement verification failed; previous helper restored.' >&2
    exit 1
fi
