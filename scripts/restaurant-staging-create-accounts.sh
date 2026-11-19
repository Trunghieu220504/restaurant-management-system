#!/bin/bash
# Fixed-name Manager/Staff provisioning; no Admin, existing-account update or SalesDay mutation.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
umask 077
mode=${1:---plan}
if [ "$#" -gt 1 ] || { [ "$mode" != --plan ] && [ "$mode" != --apply ]; } ||
   [ "$(id -u)" -ne 0 ] || [ "$(hostname)" != restaurant-staging ]; then
    echo 'Root on restaurant-staging only: --plan or --apply.' >&2; exit 2
fi
exec 9>/run/lock/restaurant-staging-accounts.lock
flock -n 9 || { echo 'Account provisioning already running.' >&2; exit 1; }
source_payload=/home/deploy/restaurant-staging-accounts.json
test -f "$source_payload" && test ! -L "$source_payload"
test "$(stat -c '%U:%a' "$source_payload")" = deploy:600
snapshot=$(mktemp /tmp/restaurant-staging-accounts.XXXXXXXX)
trap 'rm -f -- "$snapshot"' EXIT
install -o root -g root -m 0600 "$source_payload" "$snapshot"
jq -e '
  .environment == "staging" and (.accounts | length == 2) and
  ([.accounts[].userName] | sort == ["staging.manager","staging.staff"]) and
  ([.accounts[].id] | unique | length == 2) and
  all(.accounts[];
    (.id | test("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")) and
    ((.userName == "staging.manager" and .role == "Manager") or (.userName == "staging.staff" and .role == "Staff")) and
    (.passwordHash | length == 84) and (.passwordHash | test("^[A-Za-z0-9+/]+={0,2}$")) and (has("password") | not))' "$snapshot" >/dev/null
sql=restaurant-staging-sqlserver-1
database=$(docker inspect restaurant-staging-api-1 | jq -er '
  .[0].Config.Env[] | select(startswith("ConnectionStrings__Restaurant=")) | ltrimstr("ConnectionStrings__Restaurant=") |
  split(";") | map(select(test("^(Database|Initial Catalog)="; "i"))) |
  if length == 1 then .[0] | sub("^[^=]+="; "") else error("Ambiguous database") end')
[[ "$database" =~ ^[A-Za-z][A-Za-z0-9_]{0,100}$ ]]
sql_run() {
    docker exec -i "$sql" bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD";
      exec /opt/mssql-tools18/bin/sqlcmd -C -I -b -r 1 -S localhost -U sa -d "$1"' _ "$database"
}
sql_run <<'SQL' >/dev/null
SET NOCOUNT ON;
IF COL_LENGTH(N'identity.AspNetUsers',N'IsActive') IS NULL THROW 51000, 'Identity schema missing', 1;
IF (SELECT COUNT(*) FROM [identity].AspNetRoles WHERE NormalizedName IN (N'MANAGER',N'STAFF')) <> 2 THROW 51000, 'Roles missing', 1;
SQL
for key in 0 1; do
    name=$(jq -r --argjson key "$key" '.accounts[$key].userName' "$snapshot")
    id=$(jq -r --argjson key "$key" '.accounts[$key].id' "$snapshot")
    sql_run <<SQL >/dev/null
IF EXISTS (SELECT 1 FROM [identity].AspNetUsers WHERE Id = N'$id' OR NormalizedUserName = UPPER(N'$name') OR NormalizedEmail = UPPER(N'$name@example.invalid'))
    THROW 51001, 'Account exists: stop rather than overwrite or reset its password', 1;
SQL
done
echo 'Plan: verified backup, then create only staging.manager (Manager) and staging.staff (Staff).'
echo 'No Admin, existing-account modification, role-permission change, migration or SalesDay write.'
if [ "$mode" = --plan ]; then exit 0; fi
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
backup=/var/backups/restaurant-staging-accounts/$stamp
install -d -o root -g root -m 0700 "$backup"
backup_file=/var/opt/mssql/data/restaurant-staging-accounts-$stamp.bak
sql_run <<SQL > "$backup/backup.log" 2>&1
BACKUP DATABASE [$database] TO DISK = N'$backup_file' WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = N'$backup_file' WITH CHECKSUM;
SQL
docker cp "$sql:$backup_file" "$backup/database.bak" >/dev/null 2>&1
chmod 0600 "$backup/database.bak"
sha256sum "$backup/database.bak" > "$backup/database.sha256"
printf 'SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRAN;\nDECLARE @Role uniqueidentifier;\n' > "$backup/create.sql"
for key in 0 1; do
    name=$(jq -r --argjson key "$key" '.accounts[$key].userName' "$snapshot")
    id=$(jq -r --argjson key "$key" '.accounts[$key].id' "$snapshot")
    role=$(jq -r --argjson key "$key" '.accounts[$key].role' "$snapshot")
    hash=$(jq -r --argjson key "$key" '.accounts[$key].passwordHash' "$snapshot")
    cat >> "$backup/create.sql" <<SQL
IF EXISTS (SELECT 1 FROM [identity].AspNetUsers WITH (UPDLOCK,HOLDLOCK) WHERE Id = N'$id' OR NormalizedUserName = UPPER(N'$name') OR NormalizedEmail = UPPER(N'$name@example.invalid')) THROW 51001, 'Account collision', 1;
SET @Role = (SELECT Id FROM [identity].AspNetRoles WHERE NormalizedName = UPPER(N'$role'));
IF @Role IS NULL THROW 51000, 'Role missing', 1;
INSERT [identity].AspNetUsers (Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount)
VALUES (N'$id',N'$name',UPPER(N'$name'),N'$name@example.invalid',UPPER(N'$name@example.invalid'),0,N'$hash',CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()),0,0,1,0);
INSERT [identity].AspNetUserRoles (UserId,RoleId) VALUES (N'$id',@Role);
SQL
done
printf 'COMMIT;\n' >> "$backup/create.sql"
sql_run < "$backup/create.sql" > "$backup/create.log" 2>&1
echo "Created two staging accounts atomically. Backup: $backup"
echo 'Verify login and role permissions through the public API before delivering credentials.'
