# Runs HIE-1.2 checks against disposable local SQL Server.
param([switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$name = 'restaurant-foundation-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 10)
$password = 'Test!A9' + [Guid]::NewGuid().ToString('N')
$created = $false
$resultsDirectory = Join-Path 'TestResults/FoundationCurrent' ([Guid]::NewGuid().ToString('N'))
$previousConnection = $env:IDENTITY_TEST_SQLSERVER
$previousPassword = $env:MSSQL_SA_PASSWORD
Push-Location $root
try {
    docker info --format '{{.ServerVersion}}'
    if ($LASTEXITCODE -ne 0) { throw 'Docker Engine is not accessible.' }
    $env:MSSQL_SA_PASSWORD = $password
    docker run -d --name $name -p '127.0.0.1::1433' -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD mcr.microsoft.com/mssql/server:2022-latest
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create isolated SQL Server.' }
    $created = $true
    $ready = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        # Windows PowerShell promotes native stderr before SQL is ready.
        $ErrorActionPreference = 'Continue'
        try {
            docker exec -e SQLCMDPASSWORD=$password $name /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -l 1 -Q 'SELECT 1' 2>&1 | Out-Null
            $probeExit = $LASTEXITCODE
        }
        finally { $ErrorActionPreference = 'Stop' }
        if ($probeExit -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'SQL Server readiness check timed out.' }
    $binding = docker port $name '1433/tcp'
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '127\.0\.0\.1:(\d+)') { throw 'Unable to determine isolated SQL port.' }
    $port = $Matches[1]
    $env:IDENTITY_TEST_SQLSERVER = "Server=127.0.0.1,$port;User Id=sa;Password=$password;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10"
    if (-not $SkipRestore) {
        dotnet restore RestaurantManagement.slnx --locked-mode -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    }
    dotnet build RestaurantManagement.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore --logger trx --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw "Full regression failed; inspect $resultsDirectory." }
    $results = @(Get-ChildItem -LiteralPath $resultsDirectory -Filter '*.trx' -File)
    if ($results.Count -ne 2) { throw 'Expected TRX evidence for both architecture and integration suites.' }
    foreach ($result in $results) {
        [xml]$trx = Get-Content -LiteralPath $result.FullName -Raw
        $counters = $trx.TestRun.ResultSummary.Counters
        if ($null -eq $counters -or [int]$counters.total -le 0 -or
            [int]$counters.total -ne [int]$counters.passed -or
            [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0) {
            throw "Tests failed, were skipped or produced invalid counters: $($result.Name)"
        }
    }
    Write-Host "Verified zero-skip TRX results in $resultsDirectory"
}
finally {
    $env:IDENTITY_TEST_SQLSERVER = $previousConnection
    $env:MSSQL_SA_PASSWORD = $previousPassword
    if ($created) {
        docker rm -f -v $name | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Unable to remove test container $name" }
    }
    Pop-Location
}
