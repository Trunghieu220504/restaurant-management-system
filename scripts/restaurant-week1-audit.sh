#!/bin/bash
# Fixed read-only staging audit. Install root-owned; do not grant raw docker/shell sudo.
set -eu
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
if [ "$#" -ne 0 ]; then
    echo 'This audit accepts no arguments.' >&2
    exit 2
fi
if [ "$(id -u)" -ne 0 ]; then
    echo 'Run this root-owned audit through sudo.' >&2
    exit 2
fi
printf '\nSSH effective configuration\n'
/usr/sbin/sshd -T | /usr/bin/grep -E '^(permitrootlogin|passwordauthentication|kbdinteractiveauthentication|pubkeyauthentication) '
if [ -n "${SSH_CONNECTION:-}" ]; then
    read -r client_ip client_port server_ip server_port <<< "$SSH_CONNECTION"
    printf '\nSSH effective configuration for current deploy connection\n'
    /usr/sbin/sshd -T -C "user=deploy,host=restaurant-staging,addr=$client_ip,laddr=$server_ip,lport=$server_port" |
        /usr/bin/grep -E '^(permitrootlogin|passwordauthentication|kbdinteractiveauthentication|pubkeyauthentication) '
fi
printf '\nFirewall\n'
/usr/sbin/ufw status
printf '\nFail2ban SSH counts (addresses omitted)\n'
/usr/bin/fail2ban-client status sshd | /usr/bin/grep -E 'Currently failed:|Total failed:|Currently banned:|Total banned:'
printf '\nSecurity update configuration\n'
/usr/bin/apt-config dump | /usr/bin/grep -E 'APT::Periodic::(Update-Package-Lists|Unattended-Upgrade)|Unattended-Upgrade::Allowed-Origins|Unattended-Upgrade::Origins-Pattern' || true
/usr/bin/systemctl is-enabled unattended-upgrades || true
/usr/bin/systemctl is-active unattended-upgrades || true
printf '\nRecent unattended-upgrade summary\n'
if [ -f /var/log/unattended-upgrades/unattended-upgrades.log ]; then
    /usr/bin/grep -E 'Starting unattended upgrades script|No packages found that can be upgraded unattended|All upgrades installed|Packages that will be upgraded|ERROR|WARNING' /var/log/unattended-upgrades/unattended-upgrades.log | /usr/bin/tail -n 12 || true
fi
printf '\nCertificate timer\n'
/usr/bin/systemctl is-enabled certbot.timer || true
/usr/bin/systemctl list-timers --all --no-pager certbot.timer
printf '\nRenewal mechanism (no private keys)\n'
for renewal in /etc/letsencrypt/renewal/*.conf; do
    [ -f "$renewal" ] || continue
    /usr/bin/grep -E '^(authenticator|installer|server|webroot_path|pre_hook|post_hook|renew_hook) *=' "$renewal" | /usr/bin/sed -E 's/^((pre_hook|post_hook|renew_hook) *=).*/\1[configured: review before dry-run]/'
done
printf '\nContainers and published ports\n'
/usr/bin/docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
for container in restaurant-staging-api-1 restaurant-staging-web-1 restaurant-staging-sqlserver-1; do
    printf '\nContainer image and health: %s\n' "$container"
    /usr/bin/docker inspect --format '{{.Config.Image}} | image={{.Image}} | revision={{index .Config.Labels "org.opencontainers.image.revision"}} | state={{.State.Status}} | {{if .State.Health}}health={{.State.Health.Status}}{{end}} | bindings={{json .HostConfig.PortBindings}}' "$container"
done
printf '\nTrusted proxy configuration only (other environment values omitted)\n'
/usr/bin/docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' restaurant-staging-api-1 |
    /usr/bin/grep '^ReverseProxy__KnownProxies__' || true
printf '\nNginx container network addresses\n'
/usr/bin/docker inspect --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' restaurant-staging-nginx-1
printf '\nNginx routing and forwarded headers only\n'
/usr/bin/docker exec restaurant-staging-nginx-1 nginx -T 2>/dev/null |
    /usr/bin/grep -E '^ *proxy_(set_header|pass) '
