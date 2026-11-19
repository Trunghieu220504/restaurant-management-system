param([Parameter(Mandatory = $true)][string]$CredentialFile)
$ErrorActionPreference = 'Stop'
$origin = 'https://staging.quanlynh.id.vn'
$credentials = Get-Content -LiteralPath $CredentialFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($credentials.environment -ne 'staging' -or $credentials.origin -ne $origin -or
    $credentials.accounts.Count -ne 2) { throw 'Expected the dedicated staging credential file.' }
$names = @($credentials.accounts | ForEach-Object {$_.userName} | Sort-Object)
if (($names -join ',') -ne 'staging.manager,staging.staff') { throw 'Unexpected account names.' }
function Send-Request($Method, $Path, $Session, $Token, $InputObject) {
    $options = @{Method=$Method;Uri=($origin+$Path);WebSession=$Session;UseBasicParsing=$true;TimeoutSec=20;MaximumRedirection=0}
    if ($Token) {$options.Headers=@{Authorization=('Bearer '+$Token)}}
    if ($null -ne $InputObject) {$options.ContentType='application/json';$options.Body=($InputObject | ConvertTo-Json -Compress)}
    try {
        $response = Invoke-WebRequest @options
        return @{Status=[int]$response.StatusCode;Content=$response.Content;Headers=$response.Headers}
    } catch {
        if ($_.Exception.Response) {return @{Status=[int]$_.Exception.Response.StatusCode;Content=$null;Headers=$null}}
        throw 'Staging request failed; details omitted to protect credentials.'
    } finally {
        if ($options.Headers) {$options.Headers.Clear()}
        $options.Clear()
    }
}
foreach ($account in $credentials.accounts) {
    if (($account.userName -eq 'staging.manager' -and $account.role -ne 'Manager') -or
        ($account.userName -eq 'staging.staff' -and $account.role -ne 'Staff')) {throw 'Unexpected role.'}
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = $null
    try {
        $login = Send-Request 'POST' '/api/identity/login' $session $null @{userName=$account.userName;password=$account.password}
        if ($login.Status -ne 200) {throw 'Login rejected; account provisioning is not verified.'}
        $result = $login.Content | ConvertFrom-Json
        $token = $result.accessToken
        $cookie = [string]$login.Headers['Set-Cookie']
        if (-not $token -or $cookie -notmatch '(?i)httponly' -or $cookie -notmatch '(?i)secure' -or
            $cookie -notmatch '(?i)samesite=strict' -or $cookie -notmatch '(?i)path=/api/identity') {throw 'Secure refresh session missing.'}
        $me = Send-Request 'GET' '/api/identity/me' $session $token $null
        if ($me.Status -ne 200) {throw 'Current-account check failed.'}
        $current = $me.Content | ConvertFrom-Json
        if ($current.userName -ne $account.userName -or @($current.roles).Count -ne 1 -or
            $current.roles[0] -ne $account.role -or $current.permissions -contains 'identity.roles.manage' -or
            $current.permissions -contains 'identity.accounts.manage') {throw 'Unexpected roles or privilege escalation.'}
        if ($current.permissions -notcontains 'seating.sessions.manage' -or
            ($account.role -eq 'Manager' -and ($current.permissions -notcontains 'salesday.manage' -or
                $current.permissions -notcontains 'catalog.menu.manage')) -or
            ($account.role -eq 'Staff' -and $current.permissions -contains 'salesday.manage')) {throw 'Role permissions mismatch.'}
        $refresh = Send-Request 'POST' '/api/identity/refresh' $session $null $null
        if ($refresh.Status -ne 200) {throw 'Refresh failed.'}
        $result = $refresh.Content | ConvertFrom-Json
        $token = $result.accessToken
        if (-not $token) {throw 'Refreshed access token missing.'}
        $logout = Send-Request 'POST' '/api/identity/logout' $session $token $null
        if ($logout.Status -ne 204) {throw 'Logout failed.'}
        $revoked = Send-Request 'GET' '/api/identity/me' $session $token $null
        if ($revoked.Status -ne 401) {throw 'Logged-out access token remains accepted.'}
        $token = $null
        Write-Host ($account.userName + ': login, role permissions, secure cookie, refresh, logout and revocation PASS.')
    } catch {
        throw 'Dedicated staging account verification failed. Do not deliver credentials as usable yet.'
    } finally {
        if ($token) {$null = Send-Request 'POST' '/api/identity/logout' $session $token $null}
        $token=$null;$result=$null;$login=$null;$refresh=$null;$cookie=$null
    }
}
$credentials.status = 'LOGIN_VERIFIED'
$json = $credentials | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Resolve-Path -LiteralPath $CredentialFile).Path,$json,[Text.UTF8Encoding]::new($false))
Write-Host 'Both accounts verified. Deliver only the private credential file to the owner.'
