# Runs an isolated SQL Server container and Phuong week-one tests.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$containerName = 'restaurant-phuong-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 10)
$password = 'Test!A9' + [Guid]::NewGuid().ToString('N')
$created = $false
$previousSqlConnection = $env:IDENTITY_TEST_SQLSERVER
$previousSaPassword = $env:MSSQL_SA_PASSWORD
Push-Location $projectRoot
try {
    docker info --format '{{.ServerVersion}}'
    if ($LASTEXITCODE -ne 0) { throw 'Docker Engine is not accessible from this terminal.' }
    $env:MSSQL_SA_PASSWORD = $password
    docker run -d --name $containerName -p '127.0.0.1::1433' -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD mcr.microsoft.com/mssql/server:2022-latest
    if ($LASTEXITCODE -ne 0) { throw 'Unable to start SQL Server container.' }
    $created = $true
    $ready = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        $logs = docker logs $containerName 2>&1 | Out-String
        if ($logs -match 'SQL Server is now ready for client connections') { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'SQL Server did not become ready within 180 seconds.' }
    $binding = docker port $containerName '1433/tcp'
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '127\.0\.0\.1:(\d+)') { throw 'Unable to determine SQL test port.' }
    $sqlPort = $Matches[1]
    $env:IDENTITY_TEST_SQLSERVER = "Server=127.0.0.1,$sqlPort;User Id=sa;Password=$password;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10"
    dotnet restore RestaurantManagement.slnx --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build RestaurantManagement.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet test tests/Restaurant.IntegrationTests -c Release --no-build --no-restore --filter 'FullyQualifiedName~PhuongWeekOneSqlServerTests' --logger 'trx;LogFileName=phuong-week1.trx' --results-directory TestResults/PhuongWeek1
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. See TestResults/PhuongWeek1/phuong-week1.trx.' }
    Write-Host 'Week-one SQL tests passed. Results: TestResults/PhuongWeek1/phuong-week1.trx'
}
finally {
    $env:IDENTITY_TEST_SQLSERVER = $previousSqlConnection
    $env:MSSQL_SA_PASSWORD = $previousSaPassword
    if ($created) { docker rm -f -v $containerName | Out-Null }
    Pop-Location
}
