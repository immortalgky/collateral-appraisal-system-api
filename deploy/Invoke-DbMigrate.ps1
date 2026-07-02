<#
.SYNOPSIS
    Apply database migrations using the published DbUp tool. Run ONCE per
    release (from one app server or a jump box) BEFORE deploying the app.

.DESCRIPTION
    The Database tool's `migrate` command does two ordered steps internally:
      1. EF Core table migrations for every module (correct dependency order)
      2. DbUp database objects (views, stored procedures, functions)
    so it is the complete schema deploy. It is idempotent (the app also runs
    EF migrations at startup, which then find nothing pending).

    Connection string: the tool resolves it from two different config keys
    depending on the step, so this script sets BOTH env vars from the single
    -ConnectionString you pass. Nothing is written to disk or committed.

.EXAMPLE
    .\Invoke-DbMigrate.ps1 -Version 20260624-101500 -ConnectionString "Server=...;Database=CollateralAppraisal;..."
    .\Invoke-DbMigrate.ps1 -ArtifactDbPath C:\Deploy\temp\20260624-101500\database -ConnectionString $cs
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$ArtifactDbPath,
    [Parameter(Mandatory)][string]$ConnectionString,
    [string]$Environment = 'Production'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/deploy.config.ps1"

if (-not $ArtifactDbPath) {
    if (-not $Version) { throw "Provide -Version or -ArtifactDbPath." }
    $ArtifactDbPath = Join-Path (Join-Path $CasTempRoot $Version) 'database'
}

# Published Windows output includes Database.exe (apphost) next to Database.dll.
$exe = Join-Path $ArtifactDbPath 'Database.exe'
$dll = Join-Path $ArtifactDbPath 'Database.dll'
if (-not (Test-Path $dll)) { throw "No Database.dll under '$ArtifactDbPath'." }

Write-Host "Running migrations ($Environment) from $ArtifactDbPath" -ForegroundColor Cyan

# Both keys are needed: the EF step reads ConnectionStrings:Database; the DbUp
# step reads Environments:<Env>:ConnectionString. Set both from one input.
$env:ASPNETCORE_ENVIRONMENT      = $Environment
$env:ConnectionStrings__Database = $ConnectionString
Set-Item -Path "Env:\Environments__${Environment}__ConnectionString" -Value $ConnectionString

try {
    Push-Location $ArtifactDbPath   # so Configuration/appsettings.Database.json resolves
    if (Test-Path $exe) {
        & $exe migrate $Environment
    } else {
        dotnet $dll migrate $Environment
    }
    $code = $LASTEXITCODE
} finally {
    Pop-Location
    # Scrub the secret from this process's environment.
    Remove-Item Env:\ConnectionStrings__Database -ErrorAction SilentlyContinue
    Remove-Item "Env:\Environments__${Environment}__ConnectionString" -ErrorAction SilentlyContinue
}

if ($code -ne 0) { throw "Migration failed (exit $code)." }
Write-Host "Migrations applied successfully." -ForegroundColor Green
