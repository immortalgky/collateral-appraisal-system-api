#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Swap the frontend static files into the live web folder. Run ON the server.

.DESCRIPTION
    Static files have no file locks or server-owned config to preserve, so a
    clean mirror (/MIR) is correct — it removes stale hashed assets from prior
    builds. A timestamped backup is taken first for rollback.

.EXAMPLE
    .\Deploy-Web.ps1 -Version 20260624-101500
    .\Deploy-Web.ps1 -ArtifactWebPath C:\Deploy\temp\20260624-101500\web
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$ArtifactWebPath,
    [switch]$SkipBackup
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/deploy.config.ps1"

if (-not $ArtifactWebPath) {
    if (-not $Version) { throw "Provide -Version or -ArtifactWebPath." }
    $ArtifactWebPath = Join-Path (Join-Path $CasTempRoot $Version) 'web'
}
if (-not (Test-Path (Join-Path $ArtifactWebPath 'index.html'))) {
    throw "No index.html under '$ArtifactWebPath' — is this the Vite build output?"
}

function Invoke-Robocopy {
    param([string[]]$RoboArgs)
    robocopy @RoboArgs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
}

$live  = $CasWebLivePath
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Write-Host "Deploying frontend" -ForegroundColor Cyan
Write-Host "  from: $ArtifactWebPath"
Write-Host "  to  : $live"

if (-not $SkipBackup -and (Test-Path $live)) {
    $backup = Join-Path (Join-Path $CasBackupRoot $stamp) 'web'
    Write-Host "  backing up current live -> $backup"
    Invoke-Robocopy @($live, $backup, '/MIR', '/R:2', '/W:2', '/NFL', '/NDL', '/NP', '/NJH', '/NJS')
}

Invoke-Robocopy @($ArtifactWebPath, $live, '/MIR', '/R:3', '/W:5', '/NFL', '/NDL', '/NP', '/NJH', '/NJS')
Write-Host "Frontend deployed." -ForegroundColor Green
