$ErrorActionPreference='Stop'
$cases = @(
    @{Connection='Server=localhost\MSSQLSERVER01;Integrated Security=True;Command Timeout=120';Allowed=$true},
    @{Connection='Server=127.0.0.1,1433;Database=master;Integrated Security=True';Allowed=$true},
    @{Connection='Server=staging.quanlynh.id.vn;Database=master';Allowed=$false},
    @{Connection='Server=localhost;Database=RestaurantProduction';Allowed=$false},
    @{Connection='Server=localhost;Data Source=remote.example.invalid';Allowed=$false},
    @{Connection='Server=localhost;AttachDBFilename=C:\private\application.mdf';Allowed=$false},
    @{Connection='Server=localhost;Failover Partner=remote.example.invalid';Allowed=$false},
    @{Connection='Server=localhost;User Instance=True';Allowed=$false}
)
foreach ($case in $cases) {
    $accepted=$true
    try { & (Join-Path $PSScriptRoot 'Assert-WeekOneLocalSql.ps1') -ConnectionString $case.Connection } catch { $accepted=$false }
    if ($accepted -ne $case.Allowed) { throw 'SQL connection guard acceptance mismatch.' }
}
Write-Host 'SQL connection guard: 8/8 PASS (no connections were opened).'
