#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Safe in-place swap of the backend into the live IIS folder. Run ON the
    Windows app server (once per server).

.DESCRIPTION
    Sequence:
      1. Drain & unlock  - drop app_offline.htm (ANCM shuts the app down and
                           releases DLL locks) and stop the app pool.
      2. Back up         - mirror the current live folder to a timestamped
                           backup for one-command rollback.
      3. Copy            - robocopy /MIR (cleans stale DLLs) while /XF + /XD
                           preserve the server-owned config + logs.
      4. Restart         - remove app_offline.htm, start the pool, health check.

    Run Invoke-DbMigrate.ps1 ONCE (before the first server) so the schema is
    current before any new app instance starts.

.EXAMPLE
    .\Deploy-App.ps1 -Version 20260624-101500
    .\Deploy-App.ps1 -ArtifactApiPath C:\Deploy\temp\20260624-101500\api
#>
[CmdletBinding()]
param(
    [string]$Version,                         # expects C:\Deploy\temp\<Version>\api
    [string]$ArtifactApiPath,                 # ...or point straight at the api folder
    [switch]$SkipBackup
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/deploy.config.ps1"
Import-Module WebAdministration -ErrorAction Stop

# --- Resolve the source folder -------------------------------------------
if (-not $ArtifactApiPath) {
    if (-not $Version) { throw "Provide -Version or -ArtifactApiPath." }
    $ArtifactApiPath = Join-Path (Join-Path $CasTempRoot $Version) 'api'
}
if (-not (Test-Path (Join-Path $ArtifactApiPath 'Api.dll'))) {
    throw "No Api.dll under '$ArtifactApiPath' — is the artifact expanded and the path correct?"
}

$live = $CasApiLivePath
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Write-Host "Deploying backend" -ForegroundColor Cyan
Write-Host "  from: $ArtifactApiPath"
Write-Host "  to  : $live"

# robocopy returns 0-7 = success, >=8 = failure.
function Invoke-Robocopy {
    param([string[]]$RoboArgs)
    robocopy @RoboArgs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
}

# --- 1. Drain & unlock ----------------------------------------------------
$offline = Join-Path $live 'app_offline.htm'
'<html><body>Deploying, back shortly.</body></html>' | Set-Content -Path $offline -Encoding UTF8
Write-Host "  app_offline.htm placed; stopping app pool '$CasAppPoolName'..."
if ((Get-WebAppPoolState -Name $CasAppPoolName).Value -ne 'Stopped') {
    Stop-WebAppPool -Name $CasAppPoolName
}
# Give the worker process a moment to exit and release file handles.
$deadline = (Get-Date).AddSeconds(30)
while ((Get-WebAppPoolState -Name $CasAppPoolName).Value -ne 'Stopped' -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
}
Start-Sleep -Seconds 2

# --- 2. Back up -----------------------------------------------------------
if (-not $SkipBackup -and (Test-Path $live)) {
    $backup = Join-Path (Join-Path $CasBackupRoot $stamp) 'api'
    Write-Host "  backing up current live -> $backup"
    Invoke-Robocopy @($live, $backup, '/MIR', '/R:2', '/W:2', '/NFL', '/NDL', '/NP', '/NJH', '/NJS')
}

# --- 3. Copy (preserve server-owned files) --------------------------------
$roboArgs = @($ArtifactApiPath, $live, '/MIR', '/R:3', '/W:5', '/NFL', '/NDL', '/NP', '/NJH', '/NJS')
$roboArgs += '/XF'; $roboArgs += $CasPreserveFiles
$roboArgs += '/XD'; $roboArgs += ($CasPreserveDirs | ForEach-Object { Join-Path $live $_ })
Write-Host "  copying new build (preserving: $($CasPreserveFiles -join ', '))"
Invoke-Robocopy $roboArgs

# --- 4. Restart + health check -------------------------------------------
Remove-Item $offline -ErrorAction SilentlyContinue
Start-WebAppPool -Name $CasAppPoolName
Write-Host "  app pool started; checking $CasHealthUrl ..."

$ok = $false
for ($i = 1; $i -le 10; $i++) {
    Start-Sleep -Seconds 3
    try {
        $resp = Invoke-WebRequest -Uri $CasHealthUrl -UseBasicParsing -TimeoutSec 5
        if ($resp.StatusCode -eq 200) { $ok = $true; break }
    } catch { }
    Write-Host "    attempt $i: not ready yet..."
}

if ($ok) {
    Write-Host "Backend deployed and healthy." -ForegroundColor Green
} else {
    Write-Warning "Health check did not pass. Investigate, or roll back from $CasBackupRoot\$stamp\api."
    exit 1
}
