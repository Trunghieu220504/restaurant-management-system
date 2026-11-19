param([Parameter(Mandatory=$true)][string]$ConnectionString)
$builder = New-Object System.Data.Common.DbConnectionStringBuilder
$builder.set_ConnectionString($ConnectionString)
$servers = @($builder.get_Keys() | Where-Object { ($_ -replace ' ','').ToLowerInvariant() -in @('server','datasource','address','addr','networkaddress') })
$catalogs = @($builder.get_Keys() | Where-Object { ($_ -replace ' ','').ToLowerInvariant() -in @('database','initialcatalog') })
if ($servers.Count -ne 1 -or $builder[$servers[0]] -notmatch '^(localhost|127\.0\.0\.1|\.)(\\[A-Za-z0-9_-]+)?(,[0-9]{1,5})?$' -or $catalogs.Count -gt 1) { throw 'Exactly one loopback SQL server is required.' }
if ($catalogs.Count -eq 1 -and $builder[$catalogs[0]] -notin @('','master')) { throw 'An application database is not allowed.' }
foreach ($key in $builder.get_Keys()) {
    if (($key -replace ' ','').ToLowerInvariant() -in @('attachdbfilename','initialfilename','failoverpartner','userinstance')) { throw 'Database attachment, user instances and failover partners are not allowed.' }
}
