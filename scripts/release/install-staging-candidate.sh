#!/usr/bin/env bash
# Run once by the VPS operator. Installs a pinned helper; does not deploy or reset data.
set -Eeuo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
umask 077
[ "$(id -u)" = 0 ] && [ "$(hostname)" = restaurant-staging ] && [ "$#" = 1 ] || exit 2
[[ "$1" =~ ^[0-9a-f]{64}$ ]] || exit 2
source_dir=$(dirname "$(readlink -f "$0")")
[[ "$source_dir" =~ ^/home/deploy/restaurant-release-[0-9a-f]{40}$ ]] || exit 2
candidate=$(mktemp -d /var/backups/restaurant-candidate-install.XXXXXXXX)
install -d -m 0700 "$candidate/migrations"
files=(staging-candidate.sh nginx.conf release.json images.tar.gz SHA256SUMS migrations/order.txt migrations/SHA256SUMS)
for module in Identity SalesDay Catalog Seating Sales Billing Inventory Fulfillment Reporting Audit; do
    if [ -f "$source_dir/migrations/$module.sql" ]; then files+=("migrations/$module.sql"); fi
done
for file in "${files[@]}"; do
    test ! -L "$source_dir/$file" && test -f "$source_dir/$file"
    install -o root -g root -m 0600 "$source_dir/$file" "$candidate/$file"
done
printf '%s  %s\n' "$1" "$candidate/SHA256SUMS" | sha256sum -c - >/dev/null
# Validate manifest paths before handing them to sha256sum. All copied files must be covered.
python3 - "$candidate" <<'PY'
import re, sys
from pathlib import Path
root = Path(sys.argv[1])
entries = []
for line in (root / 'SHA256SUMS').read_text().splitlines():
    digest, name = line.split('  ', 1)
    assert re.fullmatch('[0-9a-f]{64}', digest)
    assert name in {'staging-candidate.sh', 'nginx.conf', 'release.json', 'images.tar.gz', 'migrations/order.txt', 'migrations/SHA256SUMS'} or re.fullmatch(r'migrations/(Identity|SalesDay|Catalog|Seating|Sales|Billing|Inventory|Fulfillment|Reporting|Audit)\.sql', name)
    entries.append(name)
assert len(entries) == len(set(entries))
assert set(entries) == {str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p != root / 'SHA256SUMS'}
PY
(cd "$candidate" && sha256sum -c SHA256SUMS >/dev/null)
revision=$(jq -er .revision "$candidate/release.json")
[[ "$revision" =~ ^[0-9a-f]{40}$ ]] && [ "$source_dir" = "/home/deploy/restaurant-release-$revision" ] || exit 2
release="/opt/restaurant/releases/$revision"
trusted=/opt/restaurant/releases/8af7ab3/compose
for directory in /opt/restaurant /opt/restaurant/releases "$trusted"; do
    test -d "$directory" && test ! -L "$directory" && test "$(stat -c '%U' "$directory")" = root
    test -z "$(find "$directory" -maxdepth 0 -perm /022 -print)"
done
if [ -e "$release" ] || [ -L "$release" ]; then echo 'Release directory already exists; inspect before retrying.' >&2; exit 1; fi
bash -n "$candidate/staging-candidate.sh"
install -d -o root -g root -m 0700 "$release" "$release/migrations" "$release/compose"
for file in "${files[@]}"; do install -o root -g root -m 0600 "$candidate/$file" "$release/$file"; done
chmod 0700 "$release/staging-candidate.sh"
for file in .env compose.backend.yml compose.backend.https.yml compose.frontend.yml compose.backend.ghcr.yml compose.frontend.ghcr.yml compose.week1-trusted-proxy.yml; do
    test -f "$trusted/$file" && test ! -L "$trusted/$file"
    test "$(stat -c '%U' "$trusted/$file")" = root
    test -z "$(find "$trusted/$file" -maxdepth 0 -perm /022 -print)"
    install -o root -g root -m 0600 "$trusted/$file" "$release/compose/$file"
done
docker load -i "$release/images.tar.gz" > "$candidate/image-load.log" 2>&1
"$release/staging-candidate.sh" --plan
wrapper="/usr/local/sbin/restaurant-deploy-approved-$revision"
printf '#!/bin/bash\nset -euo pipefail\n[ "$#" = 0 ] || exit 2\nexec %s/staging-candidate.sh --apply\n' "$release" > "$candidate/wrapper"
printf 'deploy ALL=(root) NOPASSWD: %s ""\n' "$wrapper" > "$candidate/sudoers"
visudo -cf "$candidate/sudoers" >/dev/null
install -o root -g root -m 0700 "$candidate/wrapper" "$wrapper"
install -o root -g root -m 0440 "$candidate/sudoers" "/etc/sudoers.d/restaurant-approved-$revision"
echo "Installed $revision after checksum/image/config validation. No deployment has run."
echo "Private installation evidence: $candidate"
