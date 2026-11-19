#!/bin/bash
# Fixed staging repair: enable APT periodic security updates without forcing upgrades/reboot.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then
    echo 'Run as root on restaurant-staging, with no arguments.' >&2
    exit 2
fi
exec 9>/run/lock/restaurant-week1-security-updates.lock
flock -n 9 || { echo 'Another repair is running.' >&2; exit 1; }
target=/etc/apt/apt.conf.d/99zz-restaurant-security-updates
backup=/var/backups/restaurant-week1-security-updates/$(date -u +%Y%m%dT%H%M%SZ)-$$
install -d -o root -g root -m 0700 "$backup"
existed=false
if [ -e "$target" ]; then
    cp -a -- "$target" "$backup/previous.conf"
    existed=true
fi
for timer in apt-daily.timer apt-daily-upgrade.timer; do
    systemctl is-enabled "$timer" > "$backup/$timer.enabled" 2>/dev/null || true
    systemctl is-active "$timer" > "$backup/$timer.active" 2>/dev/null || true
done
temporary=$(mktemp /etc/apt/apt.conf.d/.restaurant-security-updates.XXXXXX)
cleanup() { rm -f -- "$temporary"; }
rollback() {
    trap - ERR
    if [ "$existed" = true ]; then
        cp -a -- "$backup/previous.conf" "$target"
    else
        rm -f -- "$target"
    fi
    for timer in apt-daily.timer apt-daily-upgrade.timer; do
        if grep -qx disabled "$backup/$timer.enabled"; then systemctl disable "$timer" >/dev/null 2>&1 || true; fi
        if ! grep -qx active "$backup/$timer.active"; then systemctl stop "$timer" >/dev/null 2>&1 || true; fi
    done
    echo 'Repair failed; restored this override and original timer enable/active state where changed.' >&2
    exit 1
}
trap cleanup EXIT
cat > "$temporary" <<'CONFIG'
// Restaurant staging: periodic updates enabled, reboot remains manual.
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
Unattended-Upgrade::Automatic-Reboot "false";
CONFIG
preview=$(apt-config -c "$temporary" dump)
grep -q 'APT::Periodic::Unattended-Upgrade "1";' <<< "$preview"
grep -q 'APT::Periodic::Update-Package-Lists "1";' <<< "$preview"
origins=$(apt-config dump | grep 'Unattended-Upgrade::Allowed-Origins::')
grep -q security <<< "$origins"
trap rollback ERR
install -o root -g root -m 0644 "$temporary" "$target"
effective=$(apt-config dump)
grep -q 'APT::Periodic::Unattended-Upgrade "1";' <<< "$effective"
grep -q 'APT::Periodic::Update-Package-Lists "1";' <<< "$effective"
grep -q 'Unattended-Upgrade::Automatic-Reboot "false";' <<< "$effective"
systemctl enable --now apt-daily.timer apt-daily-upgrade.timer
systemctl is-enabled apt-daily.timer apt-daily-upgrade.timer
systemctl is-active apt-daily.timer apt-daily-upgrade.timer
trap - ERR
printf 'Configured APT periodic updates. No upgrade command or reboot was forced.\nBackup: %s\n' "$backup"
apt-config dump | grep -E 'APT::Periodic::(Update-Package-Lists|Unattended-Upgrade)|Unattended-Upgrade::Automatic-Reboot '
systemctl list-timers --all --no-pager apt-daily.timer apt-daily-upgrade.timer
