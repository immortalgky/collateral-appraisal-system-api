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
        db/         plain .sql deployment scripts for the DBA (no exe needed)
        database/   publish of the DbUp migration tool (Database.csproj) — the
                    optional fallback; omit with -SkipDbTool
        tools/      CasSecretTool — run on the app server to encrypt config
                    secrets (produces ENC:v1: values for appsettings.Production.json)

    Framework-dependent publish is portable: build on macOS, run on the Windows
    server (which needs the ASP.NET Core 9 Hosting Bundle installed).

.EXAMPLE
    pwsh deploy/Publish.ps1
    pwsh deploy/Publish.ps1 -Version 2026.06.24-1 -FrontendRepo ../collateral-appraisal-system-app
    pwsh deploy/Publish.ps1 -FrontendMode uat1        # build the SPA against .env.uat1
#>
[CmdletBinding()]
param(
    [string]$Version      = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [string]$FrontendRepo = '../collateral-appraisal-system-app',
    [string]$OutDir       = './dist-artifacts',
    [string]$Configuration = 'Release',
    # Vite mode -> which .env.<mode> file supplies VITE_API_URL. The SPA bakes the
    # API URL in at BUILD time, so this must match the target environment.
    [string]$FrontendMode = 'production',
    [switch]$SkipDbScripts,
    [switch]$SkipDbTool
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
if ($SkipDbTool) {
    Write-Host "==> Publish database tool (skipped)" -ForegroundColor Yellow
} else {
    Invoke-Step "Publish database tool" {
        dotnet publish Database/Database.csproj -c $Configuration -o $dbOut --nologo
    }
}

# --- Secret encryption tool (run on the app server to produce ENC:v1: values) --
Invoke-Step "Publish secret tool (CasSecretTool)" {
    dotnet publish tools/CasSecretTool/CasSecretTool.csproj -c $Configuration -o (Join-Path $stage 'tools') --nologo
}

# --- Database SQL scripts (what the DBA actually runs) -------------------
if ($SkipDbScripts) {
    Write-Host "==> Generate database SQL scripts (skipped)" -ForegroundColor Yellow
} else {
    Invoke-Step "Generate database SQL scripts" {
        & "$PSScriptRoot/New-DbDeploymentScripts.ps1" -Version $Version -OutDir (Join-Path $stage 'db')
    }
}

# --- Frontend ------------------------------------------------------------
# The SPA reads VITE_API_URL at BUILD time (src/config/index.ts), so the env file
# for the target environment must exist and carry the public API URL. A missing
# value silently falls back to http://localhost:3000/api and the deployed SPA
# cannot reach the backend — fail the build instead.
$feFull = (Resolve-Path $FrontendRepo).Path
$envFile = Join-Path $feFull ".env.$FrontendMode"
if (-not (Test-Path $envFile)) {
    throw "Missing $envFile. Create it on the build box with VITE_API_URL / VITE_APP_URL for the target environment (see .env.example), or pass -FrontendMode <name>."
}
if (-not (Select-String -Path $envFile -Pattern '^\s*VITE_API_URL\s*=\s*\S' -Quiet)) {
    throw "VITE_API_URL is not set in $envFile — the SPA would fall back to http://localhost:3000/api."
}
Invoke-Step "Build frontend (Vite, mode=$FrontendMode)" {
    Push-Location $feFull
    try {
        # The repo enforces pnpm (package.json preinstall: only-allow pnpm).
        pnpm install --frozen-lockfile
        pnpm exec tsc -b                            # same type-check gate as `pnpm build`
        pnpm exec vite build --mode $FrontendMode   # --mode selects .env.<mode>
    } finally { Pop-Location }
}
$feDist = Join-Path $feFull 'dist'
if (-not (Test-Path $feDist)) { throw "Frontend build produced no dist/ at $feDist" }
if (-not (Test-Path (Join-Path $feDist 'web.config'))) {
    Write-Warning "dist/web.config missing — the IIS SPA-fallback rewrite will not be deployed."
}
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
Write-Host "  contains: api/  web/  tools/  db/$(if (-not $SkipDbTool) { '  database/' })   (version $Version)"
Write-Host ""
Write-Host "Next:"
Write-Host "  1. Copy this zip to C:\Deploy\temp on each app server and expand it."
Write-Host "  2. Give db\ to the DBA — run 00_Prepare, 01_EF_01..11, 02, 03 (in that order),"
Write-Host "     with Invoke-SqlDeploy.ps1, sqlcmd or SSMS. Then run 99_Verify.sql."
Write-Host "  3. Deploy-App.ps1 / Deploy-Web.ps1 on each node, one node at a time."
