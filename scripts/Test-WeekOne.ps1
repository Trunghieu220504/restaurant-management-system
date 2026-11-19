param(
    [ValidateSet('Local','Staging')][string]$Mode = 'Local',
    [switch]$TestDataConfirmed
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/hai-week1/automation-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$saved = @{}
foreach ($key in @('IDENTITY_TEST_SQLSERVER','WEEK1_SESSION_FILE','WEEK1_BASE_URL','WEEK1_REPORT_FILE','WEEK1_TEST_DATA_CONFIRMED','API_URL','NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED')) {
    $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}
$hostProcess = $null
$webProcess = $null
$stagingStarted = $false
$nextEnvPath = Join-Path $repo 'web/next-env.d.ts'
$nextEnvBefore = [IO.File]::ReadAllBytes($nextEnvPath)
$summary = [ordered]@{ mode=$Mode; startedAt=[DateTime]::UtcNow.ToString('o'); checks=@(); status='FAIL' }
function Invoke-Checked([string]$Label, [string]$Command, [string[]]$Arguments, [string]$Directory) {
    Write-Host "$Label running"
    $log = Join-Path $run ($Label + '.log')
    Get-Command $Command -ErrorAction Stop | Out-Null
    Push-Location $Directory
    $oldPreference = $ErrorActionPreference
    try { $ErrorActionPreference='Continue'; & $Command @Arguments *> $log; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference=$oldPreference; Pop-Location }
    if ($code -ne 0) { throw "$Label failed; inspect its local log (do not publish raw credentials/logs)." }
    $summary.checks += [ordered]@{id=$Label;status='PASS'}
    Write-Host "$Label PASS"
}
function Wait-Http([string]$Url, $Process) {
    $until = [DateTime]::UtcNow.AddSeconds(90)
    do {
        if ($Process.HasExited) { throw 'Owned local server exited before readiness.' }
        try { $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3; if ($response.StatusCode -eq 200) { return } } catch {}
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $until)
    throw 'Local server readiness timed out.'
}
try {
    $env:WEEK1_REPORT_FILE = Join-Path $run 'browser-report.json'
    if ($Mode -eq 'Local') {
        if (-not $env:IDENTITY_TEST_SQLSERVER) {
            $env:IDENTITY_TEST_SQLSERVER = & (Join-Path $PSScriptRoot 'Get-WeekOneLocalSql.ps1')
        }
        & (Join-Path $PSScriptRoot 'Assert-WeekOneLocalSql.ps1') -ConnectionString $env:IDENTITY_TEST_SQLSERVER
        Invoke-Checked 'dotnet-restore' 'dotnet' @('restore','RestaurantManagement.slnx','--locked-mode') $repo
        Invoke-Checked 'dotnet-tests' 'dotnet' @('test','RestaurantManagement.slnx','-c','Release','--no-restore','--settings',(Join-Path $PSScriptRoot 'week-one-local.runsettings'),'--logger','trx','--results-directory',(Join-Path $run 'trx')) $repo
        $total = 0
        foreach ($file in Get-ChildItem (Join-Path $run 'trx') -Filter '*.trx') {
            [xml]$trx = Get-Content -LiteralPath $file.FullName
            $counts = $trx.TestRun.ResultSummary.Counters
            if ([int]$counts.failed -ne 0 -or [int]$counts.error -ne 0 -or [int]$counts.notExecuted -ne 0) { throw 'TRX contains failed/skipped tests; do not claim acceptance.' }
            $total += [int]$counts.passed
        }
        if ($total -eq 0) { throw 'No executed .NET test evidence found.' }
        $summary.dotnetPassed = $total
        Invoke-Checked 'frontend-unit' 'npm.cmd' @('test','--','--reporter=dot') (Join-Path $repo 'web')
        $env:WEEK1_SESSION_FILE = Join-Path $run 'private-session.json'
        $hostSource = Join-Path $PSScriptRoot 'WeekOneBrowserHost.cs'
        $hostProcess = Start-Process -FilePath 'dotnet' -ArgumentList @('run','--file',('"' + $hostSource + '"'),'-c','Release') -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'host.stdout.log') -RedirectStandardError (Join-Path $run 'host.stderr.log')
        $null = $hostProcess.Handle
        $until = [DateTime]::UtcNow.AddMinutes(3)
        while (-not (Test-Path -LiteralPath $env:WEEK1_SESSION_FILE)) {
            if ($hostProcess.HasExited -or [DateTime]::UtcNow -ge $until) { throw 'Local SQL/browser host failed to start; inspect private host logs.' }
            Start-Sleep -Seconds 1
        }
        $session = Get-Content -LiteralPath $env:WEEK1_SESSION_FILE -Raw | ConvertFrom-Json
        $env:API_URL = $session.apiUrl.TrimEnd('/')
        $env:NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED = 'true'
        Invoke-Checked 'frontend-build' 'npm.cmd' @('run','build') (Join-Path $repo 'web')
        $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
        $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
        $next = Join-Path $repo 'web/node_modules/next/dist/bin/next'
        $webProcess = Start-Process -FilePath 'node' -ArgumentList @(('"' + $next + '"'),'start','--hostname','127.0.0.1','--port',"$port") -WorkingDirectory (Join-Path $repo 'web') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'web.stdout.log') -RedirectStandardError (Join-Path $run 'web.stderr.log')
        $null = $webProcess.Handle
        $env:WEEK1_BASE_URL = "http://localhost:$port"
        Wait-Http "$env:WEEK1_BASE_URL/login" $webProcess
    } else {
        if (-not $TestDataConfirmed -or -not $env:WEEK1_SESSION_FILE) { throw 'Staging requires -TestDataConfirmed and a private WEEK1_SESSION_FILE with dedicated accounts.' }
        $env:WEEK1_TEST_DATA_CONFIRMED = 'true'
        $env:WEEK1_BASE_URL = 'https://staging.quanlynh.id.vn'
        $stagingStarted = $true
    }
    Invoke-Checked 'chromium-install' 'npx.cmd' @('playwright','install','chromium') (Join-Path $repo 'web')
    # Do not redirect browser progress: it emits only phase IDs and elapsed time.
    Push-Location (Join-Path $repo 'web')
    try { & node scripts/verify-week-one.mjs; $browserExit = $LASTEXITCODE } finally { Pop-Location }
    if ($browserExit -ne 0) { throw 'Real browser acceptance failed or was blocked; inspect sanitized browser-report.json.' }
    $summary.checks += [ordered]@{id='real-browser';status='PASS'}
    $summary.status = 'PASS'
} catch {
    $summary.error = $_.Exception.Message
    Write-Host $summary.error
} finally {
    if ($stagingStarted) {
        $cleanupLog = Join-Path $run 'cleanup.log'
        & ssh -T -i "$env:USERPROFILE/.ssh/restaurant-staging" -o BatchMode=yes -o ConnectTimeout=10 -o StrictHostKeyChecking=yes deploy@103.69.97.224 'sudo -n /usr/local/sbin/restaurant-week1-disable-test-users' *> $cleanupLog
        if ($LASTEXITCODE -ne 0) {
            $summary.status='FAIL'; $summary.cleanup='Exact-ID staging cleanup failed; an operator must inspect and disable the test accounts.'
        } else {
            & node (Join-Path $repo 'web/scripts/verify-week-one.mjs') --cleanup-only >> $cleanupLog 2>&1
            if ($LASTEXITCODE -ne 0) { $summary.status='FAIL'; $summary.cleanup='Post-deactivation login check failed; inspect cleanup log.' }
            else {
                $summary.cleanup='Exact three accounts disabled; correct Manager/Staff passwords rejected=401.'
                & ssh -T -i "$env:USERPROFILE/.ssh/restaurant-staging" -o BatchMode=yes -o ConnectTimeout=10 -o StrictHostKeyChecking=yes deploy@103.69.97.224 'rm -f -- /home/deploy/restaurant-week1-e2e-session.json' >> $cleanupLog 2>&1
                Remove-Item -LiteralPath $env:WEEK1_SESSION_FILE
            }
        }
    }
    if ($webProcess -and -not $webProcess.HasExited) { Stop-Process -Id $webProcess.Id }
    if ($hostProcess -and -not $hostProcess.HasExited) {
        [IO.File]::WriteAllText($env:WEEK1_SESSION_FILE + '.stop', 'stop')
        if (-not $hostProcess.WaitForExit(15000)) { $summary.status='FAIL'; $summary.cleanup='Host did not finish; preserve logs and inspect before deleting any database.' }
    }
    if ($hostProcess -and $hostProcess.HasExited -and $hostProcess.ExitCode -ne 0) {
        $summary.status='FAIL'; $summary.cleanup='Host/SQL cleanup returned a failure; inspect its private logs.'
    }
    $summary.finishedAt = [DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText((Join-Path $run 'summary.json'), ($summary | ConvertTo-Json -Depth 7), (New-Object Text.UTF8Encoding($false)))
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process') }
    [IO.File]::WriteAllBytes($nextEnvPath, $nextEnvBefore)
    Write-Host "Report: $run/summary.json"
}
if ($summary.status -ne 'PASS') { exit 1 }
