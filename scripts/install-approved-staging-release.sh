#!/bin/bash
# One-time operator bootstrap. Copies approved inputs to root-owned paths before any NOPASSWD grant.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
umask 077
if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then exit 2; fi
source_dir=/home/deploy/restaurant-release-95ab29b
release=/opt/restaurant/releases/95ab29b
app=/home/deploy/restaurant-management-system/infra
old_release=/home/deploy/hai-r8-release-7de77d4cbd15/infra
for file in restaurant-deploy-95ab29b.sh restaurant-staging-create-accounts.sh seating.sql sales.sql; do
    test -f "$source_dir/$file" && test ! -L "$source_dir/$file"
done
bash -n "$source_dir/restaurant-deploy-95ab29b.sh"
bash -n "$source_dir/restaurant-staging-create-accounts.sh"
echo "574e35a3dbbad08afff2e76d0ac9d8ee85a9ec2b8400df06f3350b27817c79a2  $source_dir/seating.sql" | sha256sum -c - >/dev/null
echo "458b581c151c6eafb96c92513b5b29b9d6b37f80281906b8ff1bfef2585068af  $source_dir/sales.sql" | sha256sum -c - >/dev/null
install -d -o root -g root -m 0700 /opt/restaurant /opt/restaurant/releases "$release" "$release/compose"
for file in restaurant-deploy-95ab29b.sh restaurant-staging-create-accounts.sh; do
    install -o root -g root -m 0700 "$source_dir/$file" "$release/$file"
done
for file in seating.sql sales.sql; do install -o root -g root -m 0600 "$source_dir/$file" "$release/$file"; done
for file in .env compose.backend.yml compose.backend.https.yml compose.frontend.yml compose.week1-trusted-proxy.yml; do
    install -o root -g root -m 0600 "$app/$file" "$release/compose/$file"
done
for file in compose.backend.ghcr.yml compose.frontend.ghcr.yml; do
    install -o root -g root -m 0600 "$old_release/$file" "$release/compose/$file"
done
"$release/restaurant-deploy-95ab29b.sh" --plan
cat > "$release/deploy-wrapper" <<'BASH'
#!/bin/bash
set -euo pipefail
if [ "$#" -ne 0 ]; then exit 2; fi
exec /opt/restaurant/releases/95ab29b/restaurant-deploy-95ab29b.sh --apply
BASH
cat > "$release/accounts-wrapper" <<'BASH'
#!/bin/bash
set -euo pipefail
if [ "$#" -ne 0 ]; then exit 2; fi
exec /opt/restaurant/releases/95ab29b/restaurant-staging-create-accounts.sh --apply
BASH
install -o root -g root -m 0700 "$release/deploy-wrapper" /usr/local/sbin/restaurant-deploy-approved-95ab29b
install -o root -g root -m 0700 "$release/accounts-wrapper" /usr/local/sbin/restaurant-create-staging-accounts
cat > "$release/sudoers" <<'SUDOERS'
deploy ALL=(root) NOPASSWD: /usr/local/sbin/restaurant-deploy-approved-95ab29b ""
deploy ALL=(root) NOPASSWD: /usr/local/sbin/restaurant-create-staging-accounts ""
SUDOERS
visudo -cf "$release/sudoers" >/dev/null
install -o root -g root -m 0440 "$release/sudoers" /etc/sudoers.d/restaurant-approved-95ab29b
echo 'Installed fixed-revision deploy/account helpers and no-argument sudo rules. No deploy or account creation has run.'
