# Windows local-test discovery only; never returns a remote server or application database.
$instanceRoot = 'HKLM:/SOFTWARE/Microsoft/Microsoft SQL Server'
$registry = Get-ItemProperty -LiteralPath "$instanceRoot/Instance Names/SQL" -ErrorAction Stop
$candidates = @()
foreach ($entry in $registry.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' }) {
    $setup = Get-ItemProperty -LiteralPath "$instanceRoot/$($entry.Value)/Setup" -ErrorAction SilentlyContinue
    if (-not $setup.Version -or ([version]$setup.Version).Major -lt 16) { continue }
    $serviceName = if ($entry.Name -eq 'MSSQLSERVER') { 'MSSQLSERVER' } else { 'MSSQL$' + $entry.Name }
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if (-not $service -or $service.Status -ne 'Running') { continue }
    $rank = if ($entry.Name -eq 'MSSQLSERVER') { 0 } elseif ($setup.Edition -like '*Developer*') { 1 } else { 2 }
    $candidates += [pscustomobject]@{Name=$entry.Name;Rank=$rank}
}
$selected = $candidates | Sort-Object Rank,Name | Select-Object -First 1
if (-not $selected) { throw 'No running local SQL Server 2022+ instance. Set IDENTITY_TEST_SQLSERVER for a loopback test instance.' }
$server = if ($selected.Name -eq 'MSSQLSERVER') { 'localhost' } else { 'localhost\' + $selected.Name }
# Shared memory avoids SQL Browser/TCP discovery for a verified local Windows service.
"Server=lpc:$server;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Command Timeout=120"
