#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build the deployable artifact bundle (backend + frontend + database tool).
    Cross-platform: runs on macOS or Windows under PowerShell 7 (pwsh).
    Uses only dotnet / npm / Copy-Item / Compress-Archive — no robocopy.

.DESCRIPTION
    Produces  <OutDir>/CAS-<Version>.zip  containing:
        api/        framework-dependent publish of Bootstrapper/Api
        web/        Vite production build of the frontend repo
        database/   publish of the DbUp migration tool (Database.csproj)

    Framework-dependent publish is portable: build on macOS, run on the Windows
    server (which needs the ASP.NET Core 9 Hosting Bundle installed).

.EXAMPLE
    pwsh deploy/Publish.ps1
    pwsh deploy/Publish.ps1 -Version 2026.06.24-1 -FrontendRepo ../collateral-appraisal-system-app
#>
[CmdletBinding()]
param(
    [string]$Version      = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [string]$FrontendRepo = '../collateral-appraisal-system-app',
    [string]$OutDir       = './dist-artifacts',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot   # deploy/ -> repo root
Set-Location $repoRoot

function Invoke-Step {
    param([string]$Name, [scriptblock]$Body)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Body
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "$Name failed (exit $LASTEXITCODE)" }
}

$stage = Join-Path $OutDir $Version
$apiOut = Join-Path $stage 'api'
$dbOut  = Join-Path $stage 'database'
$webOut = Join-Path $stage 'web'

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

# --- Backend -------------------------------------------------------------
Invoke-Step "Publish backend (Api)" {
    dotnet publish Bootstrapper/Api/Api.csproj -c $Configuration -o $apiOut --nologo
}

# --- Database tool (DbUp: EF migrations + views/procs) -------------------
Invoke-Step "Publish database tool" {
    dotnet publish Database/Database.csproj -c $Configuration -o $dbOut --nologo
}

# --- Frontend ------------------------------------------------------------
$feFull = (Resolve-Path $FrontendRepo).Path
Invoke-Step "Build frontend (Vite)" {
    Push-Location $feFull
    try {
        npm ci
        npm run build      # outputs ./dist
    } finally { Pop-Location }
}
$feDist = Join-Path $feFull 'dist'
if (-not (Test-Path $feDist)) { throw "Frontend build produced no dist/ at $feDist" }
Copy-Item $feDist $webOut -Recurse

# --- Strip config that must NEVER ship (server owns its own copy) --------
# appsettings.Production.json is generated on the server from the template.
# (appsettings.json / web.config still ship as reference but are excluded
#  from the copy by Deploy-App.ps1, so server versions always win.)
Remove-Item (Join-Path $apiOut 'appsettings.Production.json') -ErrorAction SilentlyContinue

# --- Bundle --------------------------------------------------------------
$zip = Join-Path $OutDir "CAS-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force

Write-Host ""
Write-Host "Built $zip" -ForegroundColor Green
Write-Host "  contains: api/  web/  database/   (version $Version)"
Write-Host "Next: copy this zip to C:\Deploy\temp on each app server and run Deploy-App.ps1 / Deploy-Web.ps1."
