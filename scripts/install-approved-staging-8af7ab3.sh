#!/bin/bash
# Install a single merged revision using the existing root-owned staging configuration.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
umask 077
if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then exit 2; fi
source_dir=/home/deploy/restaurant-release-8af7ab3
release=/opt/restaurant/releases/8af7ab3
trusted=/opt/restaurant/releases/95ab29b/compose
for directory in /opt/restaurant /opt/restaurant/releases "$trusted"; do
    test -d "$directory" && test ! -L "$directory"
    test "$(stat -c '%U' "$directory")" = root
    test -z "$(find "$directory" -maxdepth 0 -perm /022 -print)"
done
for directory in "$release" "$release/compose"; do
    test ! -L "$directory"
    if [ -e "$directory" ]; then
        test -d "$directory" && test "$(stat -c '%U' "$directory")" = root
        test -z "$(find "$directory" -maxdepth 0 -perm /022 -print)"
    fi
done
candidate=$(mktemp -d /var/backups/restaurant-staging-8af7ab3.XXXXXXXX)
for file in restaurant-deploy-8af7ab3.sh seating.sql sales.sql; do
    test -f "$source_dir/$file" && test ! -L "$source_dir/$file"
    install -o root -g root -m 0600 "$source_dir/$file" "$candidate/$file"
done
echo "864a963e7e1f63b0d6c84a31e84c1223ee468e5bb48aa43126688d66f32239d8  $candidate/restaurant-deploy-8af7ab3.sh" | sha256sum -c - >/dev/null
echo "574e35a3dbbad08afff2e76d0ac9d8ee85a9ec2b8400df06f3350b27817c79a2  $candidate/seating.sql" | sha256sum -c - >/dev/null
echo "458b581c151c6eafb96c92513b5b29b9d6b37f80281906b8ff1bfef2585068af  $candidate/sales.sql" | sha256sum -c - >/dev/null
bash -n "$candidate/restaurant-deploy-8af7ab3.sh"
exec 9>/run/lock/restaurant-staging-deploy.lock
flock -n 9 || { echo 'A deployment is already running; stop.' >&2; exit 1; }
install -d -o root -g root -m 0700 "$release" "$release/compose"
for file in restaurant-deploy-8af7ab3.sh seating.sql sales.sql; do
    test ! -L "$release/$file"
    if [ -f "$release/$file" ]; then install -o root -g root -m 0600 "$release/$file" "$candidate/previous-$file"; fi
    install -o root -g root -m 0600 "$candidate/$file" "$release/$file"
done
chmod 0700 "$release/restaurant-deploy-8af7ab3.sh"
for file in .env compose.backend.yml compose.backend.https.yml compose.frontend.yml compose.backend.ghcr.yml compose.frontend.ghcr.yml compose.week1-trusted-proxy.yml; do
    test -f "$trusted/$file" && test ! -L "$trusted/$file"
    test "$(stat -c '%U' "$trusted/$file")" = root
    test -z "$(find "$trusted/$file" -maxdepth 0 -perm /022 -print)"
    test ! -L "$release/compose/$file"
    install -o root -g root -m 0600 "$trusted/$file" "$release/compose/$file"
done
flock -u 9
"$release/restaurant-deploy-8af7ab3.sh" --plan
cat > "$candidate/deploy-wrapper" <<'BASH'
#!/bin/bash
set -euo pipefail
if [ "$#" -ne 0 ]; then exit 2; fi
exec /opt/restaurant/releases/8af7ab3/restaurant-deploy-8af7ab3.sh --apply
BASH
cat > "$candidate/sudoers" <<'SUDOERS'
deploy ALL=(root) NOPASSWD: /usr/local/sbin/restaurant-deploy-approved-8af7ab3 ""
SUDOERS
visudo -cf "$candidate/sudoers" >/dev/null
install -o root -g root -m 0700 "$candidate/deploy-wrapper" /usr/local/sbin/restaurant-deploy-approved-8af7ab3
install -o root -g root -m 0440 "$candidate/sudoers" /etc/sudoers.d/restaurant-approved-8af7ab3
echo 'Installed one fixed 8af7ab3 release helper; read-only plan passed. No deployment or account change has run.'
echo "Private installation evidence: $candidate"
