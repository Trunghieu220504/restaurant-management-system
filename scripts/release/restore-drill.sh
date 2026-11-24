#!/usr/bin/env bash
# Restores only into a fresh, generated drill database; NEVER replaces the application DB.
set -euo pipefail
# shellcheck source=common.sh
source /usr/local/lib/restaurant/common.sh
[ "$#" -eq 1 ] || exit 2
object=$1
[[ "$object" =~ ^[A-Za-z][A-Za-z0-9_]{0,63}-[0-9]{8}T[0-9]{6}Z-[0-9]+\.bak\.age$ ]] || exit 2
: "${AGE_IDENTITY_FILE:?}"
exec 9>/run/lock/restaurant-restore.lock
flock -n 9 || exit 1
work=$(mktemp -d "$BACKUP_DIRECTORY/restore.XXXXXXXX")
database="RestaurantRestore_$(date -u +%Y%m%dT%H%M%S)_$$"
container_path="/var/opt/mssql/backup/$database.bak"
cleanup() {
    docker exec "$SQL_CONTAINER" rm -f -- "$container_path" >/dev/null 2>&1 || true
    rm -f -- "$work/$object" "$work/$object.sha256" "$work/restore.bak" "$work/files.txt"
    rmdir "$work" 2>/dev/null || true
}
trap cleanup EXIT
rclone --config "$RCLONE_CONFIG" copyto "$BACKUP_REMOTE/$object" "$work/$object"
rclone --config "$RCLONE_CONFIG" copyto "$BACKUP_REMOTE/$object.sha256" "$work/$object.sha256"
# Parse only the digest, never let an untrusted checksum file specify a filesystem path.
expected=$(awk 'NR==1 {print $1}' "$work/$object.sha256")
[[ "$expected" =~ ^[0-9a-f]{64}$ ]]
[ "$(sha256sum "$work/$object" | cut -d ' ' -f 1)" = "$expected" ]
age -d -i "$AGE_IDENTITY_FILE" -o "$work/restore.bak" "$work/$object"
docker exec "$SQL_CONTAINER" mkdir -p /var/opt/mssql/backup
docker cp "$work/restore.bak" "$SQL_CONTAINER:$container_path"
docker exec -u 0 "$SQL_CONTAINER" chown mssql:mssql "$container_path"
printf "RESTORE VERIFYONLY FROM DISK=N'%s' WITH CHECKSUM;\nGO\n" "$container_path" | sql_run >/dev/null
# Let SQL enumerate ALL logical files (including multiple data files), with quoted names.
printf "
DECLARE @files TABLE (LogicalName nvarchar(128),PhysicalName nvarchar(260),Type char(1),FileGroupName nvarchar(128),Size numeric(20,0),MaxSize numeric(20,0),FileId bigint,CreateLSN numeric(25,0),DropLSN numeric(25,0),UniqueId uniqueidentifier,ReadOnlyLSN numeric(25,0),ReadWriteLSN numeric(25,0),BackupSizeInBytes bigint,SourceBlockSize int,FileGroupId int,LogGroupGUID uniqueidentifier,DifferentialBaseLSN numeric(25,0),DifferentialBaseGUID uniqueidentifier,IsReadOnly bit,IsPresent bit,TDEThumbprint varbinary(32),SnapshotURL nvarchar(360));
INSERT @files EXEC(N'RESTORE FILELISTONLY FROM DISK=N''%s''');
IF DB_ID(N'%s') IS NOT NULL THROW 51000,'Drill database already exists',1;
IF EXISTS(SELECT 1 FROM @files WHERE Type NOT IN ('D','L')) THROW 51000,'Unsupported backup file type',1;
DECLARE @sql nvarchar(max)=N'RESTORE DATABASE [%s] FROM DISK=N''%s'' WITH CHECKSUM, RECOVERY';
SELECT @sql+=N', MOVE '+QUOTENAME(LogicalName,CHAR(39))+N' TO '+QUOTENAME(N'/var/opt/mssql/data/%s_'+CONVERT(nvarchar(20),FileId)+CASE Type WHEN 'L' THEN N'.ldf' ELSE N'.mdf' END,CHAR(39)) FROM @files;
EXEC(@sql);
DBCC CHECKDB ([%s]) WITH NO_INFOMSGS;
GO
" "$container_path" "$database" "$database" "$container_path" "$database" "$database" | sql_run
printf '{"status":"DATABASE_RESTORED","database":"%s","object":"%s","utc":"%s","apiProbe":"PENDING"}\n' "$database" "$object" "$(date -u +%FT%TZ)"
# Keep the isolated DB for the API probe; operator drops only this exact generated name afterwards.
