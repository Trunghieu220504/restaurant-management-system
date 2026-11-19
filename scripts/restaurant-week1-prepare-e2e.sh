#!/bin/bash
# One-time, bounded staging test-account preparation; never deletes/resets SalesDay.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
mode=${1:---plan}
if [ "$#" -gt 1 ] || { [ "$mode" != --plan ] && [ "$mode" != --prepare-test-data ]; } ||
   [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then
    echo 'Root on restaurant-staging only: --plan or --prepare-test-data (confirms staging contains test data).' >&2
    exit 2
fi
exec 9>/run/lock/restaurant-week1-e2e.lock
flock -n 9 || { echo 'Another preparation is running.' >&2; exit 1; }
payload=/home/deploy/restaurant-week1-e2e-session.json
cleanup=/usr/local/sbin/restaurant-week1-disable-test-users
sudoers=/etc/sudoers.d/restaurant-week1-e2e-cleanup
sql=restaurant-staging-sqlserver-1
test -f "$payload" && test ! -L "$payload"
test "$(stat -c '%a' "$payload")" = 600
test "$(stat -c '%U' "$payload")" = deploy
# Validate and consume one immutable root-owned snapshot, not a mutable deploy file.
snapshot=$(mktemp /tmp/restaurant-week1-e2e.XXXXXXXX)
install -o root -g root -m 0600 "$payload" "$snapshot"
payload=$snapshot
trap 'rm -f -- "$payload"' EXIT
test ! -e "$cleanup" && test ! -L "$cleanup"
test ! -e "$sudoers" && test ! -L "$sudoers"
jq -e '
  .environment == "staging" and (.accounts | keys == ["lockout","manager","staff"]) and
  .accounts.manager.role == "Manager" and .accounts.staff.role == "Staff" and .accounts.lockout.role == "Staff" and
  ([.accounts[].id] | unique | length == 3) and ([.accounts[].userName] | unique | length == 3) and
  all(.accounts[];
    (.id | test("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")) and
    (.userName | test("^w1-e2e-[a-z0-9-]{8,50}$")) and
    (.passwordHash | length == 84) and (.passwordHash | test("^[A-Za-z0-9+/]+={0,2}$")) and
    (.password | length >= 12))' "$payload" >/dev/null
connection=${SSH_CONNECTION:-}
if [ -z "$connection" ]; then echo 'Preserve SSH_CONNECTION when calling sudo; do not guess the client address.' >&2; exit 1; fi
read -r client client_port server server_port extra <<< "$connection"
if [ -n "${extra:-}" ] || ! [[ "$client" =~ ^[0-9a-fA-F:.]+$ && "$server" =~ ^[0-9a-fA-F:.]+$ && "$server_port" =~ ^[0-9]+$ ]]; then
    echo 'Invalid SSH connection context.' >&2; exit 1
fi
test "$(/usr/sbin/sshd -T | awk '$1 == "usedns" {print $2}')" = no
for user in deploy root; do
    settings=$(/usr/sbin/sshd -T -C "user=$user,host=$client,addr=$client,laddr=$server,lport=$server_port")
    for required in 'permitrootlogin no' 'passwordauthentication no' 'kbdinteractiveauthentication no' 'pubkeyauthentication yes'; do
        grep -Fqx "$required" <<< "$settings"
    done
    printf 'SSH Match context for %s: root/password/kbd-interactive disabled; pubkey enabled (addresses omitted).\n' "$user"
done
test "$(docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' restaurant-staging-api-1)" = 7de77d4cbd15deb474bab146ae6f14dd05937dbe
database=$(docker inspect restaurant-staging-api-1 | jq -er '
  .[0].Config.Env[] | select(startswith("ConnectionStrings__Restaurant=")) | ltrimstr("ConnectionStrings__Restaurant=") |
  split(";") | map(select(test("^(Database|Initial Catalog)="; "i"))) |
  if length == 1 then .[0] | sub("^[^=]+="; "") else error("Ambiguous database") end')
[[ "$database" =~ ^[A-Za-z][A-Za-z0-9_]{0,100}$ ]]
sql_run() {
    docker exec -i "$sql" bash -c '
      export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
      exec /opt/mssql-tools18/bin/sqlcmd -C -I -b -r 1 -S localhost -U sa -d "$1"' _ "$database"
}
sql_run <<'SQL' >/dev/null
SET NOCOUNT ON;
IF COL_LENGTH(N'identity.AspNetUsers', N'IsActive') IS NULL THROW 51000, 'Identity schema missing', 1;
IF (SELECT COUNT(*) FROM [identity].AspNetRoles WHERE NormalizedName IN (N'MANAGER',N'STAFF')) <> 2 THROW 51000, 'Expected roles missing', 1;
IF OBJECT_ID(N'salesday.NgayBan') IS NULL THROW 51000, 'SalesDay schema missing', 1;
SQL
for key in manager staff lockout; do
    name=$(jq -r --arg key "$key" '.accounts[$key].userName' "$payload")
    id=$(jq -r --arg key "$key" '.accounts[$key].id' "$payload")
    sql_run <<SQL >/dev/null
IF EXISTS (SELECT 1 FROM [identity].AspNetUsers WHERE Id = N'$id' OR NormalizedUserName = UPPER(N'$name') OR NormalizedEmail = UPPER(N'$name@example.invalid'))
    THROW 51001, 'Dedicated test identity already exists; do not overwrite', 1;
SQL
done
echo 'Plan: backup/verify current DB; add three dedicated test users (Manager, Staff, lockout Staff).'
echo 'Install cleanup sudo permission only for a root-owned no-argument helper disabling those exact three IDs.'
echo 'No bootstrap Admin, migration, image deployment or SalesDay reset/close/delete.'
if [ "$mode" = --plan ]; then exit 0; fi
umask 077
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
backup=/var/backups/restaurant-week1-e2e/$stamp
install -d -o root -g root -m 0700 "$backup"
backup_file=/var/opt/mssql/data/restaurant-week1-e2e-$stamp.bak
sql_run <<SQL > "$backup/backup.log" 2>&1
BACKUP DATABASE [$database] TO DISK = N'$backup_file' WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = N'$backup_file' WITH CHECKSUM;
SQL
docker cp "$sql:$backup_file" "$backup/database.bak" >/dev/null 2>&1
chmod 0600 "$backup/database.bak"
sha256sum "$backup/database.bak" > "$backup/database.sha256"
printf 'SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; BEGIN TRAN;\nDECLARE @Role uniqueidentifier;\n' > "$backup/create.sql"
printf 'SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; BEGIN TRAN;\n' > "$backup/disable.sql"
for key in manager staff lockout; do
    name=$(jq -r --arg key "$key" '.accounts[$key].userName' "$payload")
    id=$(jq -r --arg key "$key" '.accounts[$key].id' "$payload")
    role=$(jq -r --arg key "$key" '.accounts[$key].role' "$payload")
    hash=$(jq -r --arg key "$key" '.accounts[$key].passwordHash' "$payload")
    cat >> "$backup/create.sql" <<SQL
IF EXISTS (SELECT 1 FROM [identity].AspNetUsers WITH (UPDLOCK,HOLDLOCK) WHERE Id = N'$id' OR NormalizedUserName = UPPER(N'$name') OR NormalizedEmail = UPPER(N'$name@example.invalid')) THROW 51001, 'Test identity collision', 1;
SET @Role = (SELECT Id FROM [identity].AspNetRoles WHERE NormalizedName = UPPER(N'$role'));
IF @Role IS NULL THROW 51000, 'Role missing', 1;
INSERT [identity].AspNetUsers (Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount)
VALUES (N'$id',N'$name',UPPER(N'$name'),N'$name@example.invalid',UPPER(N'$name@example.invalid'),0,N'$hash',CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()),0,0,1,0);
INSERT [identity].AspNetUserRoles (UserId,RoleId) VALUES (N'$id',@Role);
SQL
    cat >> "$backup/disable.sql" <<SQL
UPDATE [identity].AspNetUsers SET IsActive = 0, SecurityStamp = CONVERT(nvarchar(36),NEWID()) WHERE Id = N'$id' AND NormalizedUserName = UPPER(N'$name');
IF @@ROWCOUNT <> 1 THROW 51002, 'Exact test identity missing; cleanup stopped', 1;
SQL
done
printf 'COMMIT;\n' >> "$backup/create.sql"
printf 'COMMIT;\n' >> "$backup/disable.sql"
cat > "$backup/disable-helper" <<'HEADER'
#!/bin/bash
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then exit 2; fi
HEADER
printf 'database=%q\n' "$database" >> "$backup/disable-helper"
cat >> "$backup/disable-helper" <<'COMMAND'
docker exec -i restaurant-staging-sqlserver-1 bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -C -I -b -r 1 -S localhost -U sa -d "$1"' _ "$database" <<'SQL'
COMMAND
cat "$backup/disable.sql" >> "$backup/disable-helper"
printf 'SQL\nprintf "Exact three test users disabled; stamps rotated. No SalesDay data changed.\\n"\n' >> "$backup/disable-helper"
bash -n "$backup/disable-helper"
chmod 0700 "$backup/disable-helper"
printf 'deploy ALL=(root) NOPASSWD: /usr/local/sbin/restaurant-week1-disable-test-users ""\n' > "$backup/sudoers"
visudo -cf "$backup/sudoers" >/dev/null
rollback() {
    trap - ERR
    set +e
    "$backup/disable-helper" > "$backup/failed-preparation-cleanup.log" 2>&1
    rm -f -- "$cleanup" "$sudoers"
    echo "Preparation failed. Exact-ID cleanup attempted; inspect private backup: $backup" >&2
    exit 1
}
trap rollback ERR
sql_run < "$backup/create.sql" > "$backup/create.log" 2>&1
install -o root -g root -m 0700 "$backup/disable-helper" "$cleanup"
install -o root -g root -m 0440 "$backup/sudoers" "$sudoers"
trap - ERR
printf 'Three dedicated test accounts created. Private DB backup/checksum: %s\n' "$backup"
echo 'Use private session file in the automated browser runner; do not paste passwords/hashes.'
echo 'After testing, run: sudo -n /usr/local/sbin/restaurant-week1-disable-test-users'
