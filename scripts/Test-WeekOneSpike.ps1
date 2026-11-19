param([ValidateRange(20,100)][int]$Repeat = 20)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/hai-week1/spike-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$previous = $env:IDENTITY_TEST_SQLSERVER
$results = @()
try {
    if (-not $env:IDENTITY_TEST_SQLSERVER) { $env:IDENTITY_TEST_SQLSERVER = & (Join-Path $PSScriptRoot 'Get-WeekOneLocalSql.ps1') }
    & (Join-Path $PSScriptRoot 'Assert-WeekOneLocalSql.ps1') -ConnectionString $env:IDENTITY_TEST_SQLSERVER
    Push-Location $repo
    try {
        foreach ($iteration in 1..$Repeat) {
            $folder = Join-Path $run ('run-' + $iteration.ToString('00'))
            New-Item -ItemType Directory -Path $folder | Out-Null
            $oldPreference=$ErrorActionPreference
            try {
                $ErrorActionPreference='Continue'
                & dotnet test tests/Restaurant.IntegrationTests -c Release --no-build --no-restore --filter 'FullyQualifiedName~PhuongWeekOneSqlServerTests' --logger 'trx;LogFileName=spike.trx' --results-directory $folder *> (Join-Path $folder 'test.log')
                $code=$LASTEXITCODE
            } finally { $ErrorActionPreference=$oldPreference }
            if ($code -ne 0) { throw "Spike iteration $iteration failed; inspect its local TRX/log." }
            [xml]$trx = Get-Content -LiteralPath (Join-Path $folder 'spike.trx')
            $counters = $trx.TestRun.ResultSummary.Counters
            if ([int]$counters.passed -ne 9 -or [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or [int]$counters.total -ne 9) { throw "Spike iteration $iteration did not execute all nine tests successfully." }
            $results += [ordered]@{iteration=$iteration;passed=9;failed=0;skipped=0}
            Write-Host "Spike $iteration/$Repeat PASS: 9/9, no skip"
        }
    } finally { Pop-Location }
} finally {
    $env:IDENTITY_TEST_SQLSERVER = $previous
    [IO.File]::WriteAllText((Join-Path $run 'summary.json'), (@{completed=$results.Count;requested=$Repeat;passedTests=9*$results.Count;iterations=$results} | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
    Write-Host "Spike report: $run/summary.json"
}
