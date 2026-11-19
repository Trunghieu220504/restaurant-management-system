#!/bin/bash
# Existing staging only: test renewal, then align the packaged timer with Nginx's certificate.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
mode=${1:---plan}
if [ "$#" -gt 1 ] || { [ "$mode" != --plan ] && [ "$mode" != --apply ]; } ||
   [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then
    echo 'Run as root on restaurant-staging: --plan (default) or --apply.' >&2
    exit 2
fi
exec 9>/run/lock/restaurant-week1-certbot.lock
flock -n 9 || { echo 'Another certificate repair is running.' >&2; exit 1; }
work=/home/deploy/restaurant-management-system/infra/certbot
conf=$work/conf
name=staging.quanlynh.id.vn
renewal=$conf/renewal/$name.conf
nginx=restaurant-staging-nginx-1
hook=/usr/local/sbin/restaurant-staging-reload-certificate
dropin=/etc/systemd/system/certbot.service.d/restaurant-staging.conf
for command in certbot docker jq openssl systemctl curl; do command -v "$command" >/dev/null; done
test "$(find /etc/letsencrypt/renewal -maxdepth 1 -type f -name '*.conf' 2>/dev/null | wc -l)" -eq 0
test "$(find "$conf/renewal" -maxdepth 1 -type f -name '*.conf' | wc -l)" -eq 1
test -f "$renewal"
test ! -L "$renewal"
grep -qx 'authenticator = webroot' "$renewal"
grep -Fqx "$name = $work/www" "$renewal"
if grep -Eq '^[[:space:]]*(pre_hook|post_hook|renew_hook|deploy_hook)[[:space:]]*=' "$renewal"; then
    echo 'Unexpected saved renewal hooks; review manually. Nothing changed.' >&2
    exit 1
fi
if [ -d "$conf/renewal-hooks" ] && [ -n "$(find "$conf/renewal-hooks" -type f -print -quit)" ]; then
    echo 'Unexpected directory hooks; review manually. Nothing changed.' >&2
    exit 1
fi
test -z "$(systemctl show certbot.service -p DropInPaths --value)"
test ! -e "$hook" && test ! -L "$hook"
test ! -e "$dropin" && test ! -L "$dropin"
test "$(systemctl is-active certbot.service || true)" != active
test "$(systemctl is-active certbot.service || true)" != activating
systemctl is-enabled --quiet certbot.timer
systemctl is-active --quiet certbot.timer
docker inspect "$nginx" | jq -e --arg conf "$conf" --arg www "$work/www" '
  .[0] | .State.Running and
  any(.Mounts[]; .Type == "bind" and .Source == $conf and .Destination == "/etc/letsencrypt" and .RW == false) and
  any(.Mounts[]; .Type == "bind" and .Source == $www and .Destination == "/var/www/certbot" and .RW == false)' >/dev/null
docker exec "$nginx" nginx -t
openssl x509 -in "$conf/live/$name/fullchain.pem" -noout -checkhost "$name" >/dev/null
before_cert=$(openssl x509 -in "$conf/live/$name/fullchain.pem" -noout -fingerprint -sha256)
before_nginx=$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$nginx")
base=(/usr/bin/certbot --config /dev/null renew --config-dir "$conf" --work-dir "$work/lib"
    --logs-dir "$work/logs" --cert-name "$name" --no-directory-hooks --no-random-sleep-on-renew)
if [ "$mode" = --plan ]; then
    echo 'Plan: test only the existing staging certificate using the ACME staging server.'
    echo 'Then install a fixed Nginx test/reload hook and a certbot.service drop-in for the mounted project certificate.'
    echo 'No live certificate request, container recreation, SQL operation or reboot. Timer must remain active.'
    exit 0
fi
umask 077
backup=/var/backups/restaurant-week1-certbot/$(date -u +%Y%m%dT%H%M%SZ)-$$
install -d -o root -g root -m 0700 "$backup"
cp -a -- "$renewal" "$backup/renewal.conf"
systemctl cat certbot.service > "$backup/certbot.service.before.txt"
systemctl cat certbot.timer > "$backup/certbot.timer.before.txt"
cat > "$backup/reload-hook" <<'HOOK'
#!/bin/bash
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then exit 2; fi
docker inspect restaurant-staging-nginx-1 | jq -e '.[0] | .State.Running and
  any(.Mounts[]; .Type == "bind" and .Source == "/home/deploy/restaurant-management-system/infra/certbot/conf" and
      .Destination == "/etc/letsencrypt" and .RW == false)' >/dev/null
docker exec restaurant-staging-nginx-1 nginx -t
docker exec restaurant-staging-nginx-1 nginx -s reload
HOOK
cat > "$backup/service-dropin" <<'UNIT'
[Service]
ExecStart=
ExecStart=/usr/bin/certbot --config /dev/null -q renew --no-random-sleep-on-renew --config-dir /home/deploy/restaurant-management-system/infra/certbot/conf --work-dir /home/deploy/restaurant-management-system/infra/certbot/lib --logs-dir /home/deploy/restaurant-management-system/infra/certbot/logs --cert-name staging.quanlynh.id.vn --no-directory-hooks --deploy-hook /usr/local/sbin/restaurant-staging-reload-certificate
UNIT
bash -n "$backup/reload-hook"
rollback() {
    trap - ERR
    set +e
    rm -f -- "$dropin" "$hook"
    cp -a -- "$backup/renewal.conf" "$renewal"
    systemctl daemon-reload > "$backup/rollback.log" 2>&1
    echo "Repair failed; removed new scheduler/hook and restored renewal configuration. Private log: $backup" >&2
    exit 1
}
trap rollback ERR
# Certbot's own lineage lock prevents overlap with other Certbot invocations.
# Disable default CLI ini settings and directory hooks; saved hooks were rejected above.
if ! "${base[@]}" --dry-run --non-interactive --server https://acme-staging-v02.api.letsencrypt.org/directory > "$backup/dry-run.log" 2>&1; then
    echo "ACME staging dry-run failed. Do not publish the private log; inspect locally: $backup/dry-run.log" >&2
    false
fi
test "$(openssl x509 -in "$conf/live/$name/fullchain.pem" -noout -fingerprint -sha256)" = "$before_cert"
cmp -s "$renewal" "$backup/renewal.conf"
install -o root -g root -m 0700 "$backup/reload-hook" "$hook"
# Test the same hook used after future successful renewals, without recreating Nginx.
"$hook" > "$backup/hook-test.log" 2>&1
test "$(docker inspect --format '{{.Id}} {{.State.StartedAt}}' "$nginx")" = "$before_nginx"
curl --fail --silent --show-error --max-time 15 -o /dev/null "https://$name/health/ready"
install -d -o root -g root -m 0755 "$(dirname "$dropin")"
install -o root -g root -m 0644 "$backup/service-dropin" "$dropin"
systemctl daemon-reload
systemctl show certbot.service -p ExecStart --value | grep -Fq -- "--config-dir $conf"
systemctl show certbot.service -p ExecStart --value | grep -Fq -- "--deploy-hook $hook"
systemctl is-enabled --quiet certbot.timer
systemctl is-active --quiet certbot.timer
test "$(openssl x509 -in "$conf/live/$name/fullchain.pem" -noout -fingerprint -sha256)" = "$before_cert"
trap - ERR
echo 'ACME staging dry-run passed; active certificate unchanged; fixed reload hook passed; ready=200.'
echo 'certbot.timer remains enabled/active and now invokes the mounted certificate configuration plus reload hook.'
printf 'Private backup/logs: %s\n' "$backup"
systemctl list-timers certbot.timer --no-pager
