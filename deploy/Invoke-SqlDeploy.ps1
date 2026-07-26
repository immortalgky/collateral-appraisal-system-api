<#
.SYNOPSIS
    Apply the generated SQL deployment bundle with sqlcmd. Run ONCE per release,
    BEFORE deploying the application to any node. No Database.exe required.

.DESCRIPTION
    Executes, in order and aborting on the first error (-b):

        00_Prepare.sql
        01_EF_01..11_*.sql          (EF Core schema, module dependency order)
        02_Repeatable_ViewsAndProcs.sql
        03_OneTime_DataScripts.sql

    Every file is idempotent, so a failed run can be fixed and re-run.
    99_Verify.sql is NOT run automatically — run it afterwards and read the output.

    Authentication: pass -TrustedConnection for Windows/AD auth (recommended), or
    -Username/-Password for a SQL login. The password is never written to disk.

.EXAMPLE
    .\Invoke-SqlDeploy.ps1 -ServerInstance SQLHOST -Database CollateralAppraisal -TrustedConnection
    .\Invoke-SqlDeploy.ps1 -ServerInstance SQLHOST\PROD -Database CollateralAppraisal -Username cas_deploy -Password (Read-Host -AsSecureString)
    .\Invoke-SqlDeploy.ps1 -ScriptPath C:\Deploy\temp\20260723-101500\db -ServerInstance SQLHOST -Database CollateralAppraisal -TrustedConnection -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$ServerInstance,
    [Parameter(Mandatory)][string]$Database,
    [string]$ScriptPath,                       # defaults to <version>\db under the staging root
    [string]$Version,
    [switch]$TrustedConnection,
    [string]$Username,
    [System.Security.SecureString]$Password,
    [switch]$TrustServerCertificate,
    [int]$QueryTimeoutSeconds = 1800,
    [string]$LogDirectory
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/deploy.config.ps1"

if (-not $ScriptPath) {
    if (-not $Version) { throw 'Provide -ScriptPath or -Version.' }
    $ScriptPath = Join-Path (Join-Path $CasTempRoot $Version) 'db'
}
if (-not (Test-Path $ScriptPath)) { throw "No script folder at '$ScriptPath'." }

if (-not $TrustedConnection -and -not $Username) {
    throw 'Specify -TrustedConnection (Windows auth) or -Username/-Password (SQL auth).'
}
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'sqlcmd not found. Install "Microsoft ODBC Driver + sqlcmd utility" (or SSMS) on this machine.'
}

# Deterministic run order: 00, then 01_EF_* by their two-digit index, then 02, 03.
$files = @(Get-ChildItem -Path $ScriptPath -Filter '*.sql' -File |
    Where-Object { $_.Name -notlike '99_*' } |
    Sort-Object Name)
if ($files.Count -eq 0) { throw "No .sql files in '$ScriptPath'." }

if (-not $LogDirectory) { $LogDirectory = Join-Path $ScriptPath 'logs' }
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss'

Write-Host "Database deploy" -ForegroundColor Cyan
Write-Host "  server : $ServerInstance"
Write-Host "  database: $Database"
Write-Host "  scripts: $ScriptPath  ($($files.Count) files)"
Write-Host "  logs   : $LogDirectory"

$auth = @()
if ($TrustedConnection) {
    $auth += '-E'
} else {
    $plain = [System.Net.NetworkCredential]::new('', $Password).Password
    if (-not $plain) { throw '-Password is required with -Username.' }
    $auth += @('-U', $Username, '-P', $plain)
}
if ($TrustServerCertificate) { $auth += '-C' }

try {
    foreach ($f in $files) {
        if (-not $PSCmdlet.ShouldProcess($f.Name, "sqlcmd against $ServerInstance/$Database")) { continue }
        $log = Join-Path $LogDirectory "$runStamp`_$($f.BaseName).log"
        Write-Host "  -> $($f.Name)" -ForegroundColor Cyan
        $sw = [Diagnostics.Stopwatch]::StartNew()
        # -b: exit non-zero on SQL error.  -V 16: treat >=16 severity as failure.
        & sqlcmd -S $ServerInstance -d $Database @auth `
                 -i $f.FullName -o $log `
                 -b -V 16 -t $QueryTimeoutSeconds
        $code = $LASTEXITCODE
        $sw.Stop()
        if ($code -ne 0) {
            Write-Host ''
            Write-Warning "FAILED on $($f.Name) after $([int]$sw.Elapsed.TotalSeconds)s — last lines of $log :"
            Get-Content $log -Tail 25 | ForEach-Object { Write-Host "    $_" }
            throw "sqlcmd exit $code on $($f.Name). Fix the cause and re-run this script (all files are idempotent)."
        }
        Write-Host ("     ok ({0}s)" -f [int]$sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
    }
} finally {
    $plain = $null
    [GC]::Collect()
}

Write-Host ''
Write-Host 'Database deployed. Now run 99_Verify.sql and review its output:' -ForegroundColor Green
Write-Host "  sqlcmd -S $ServerInstance -d $Database $(if($TrustedConnection){'-E'}else{"-U $Username"}) -i `"$(Join-Path $ScriptPath '99_Verify.sql')`""
