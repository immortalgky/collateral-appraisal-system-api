<#
.SYNOPSIS
    Shared deployment settings for the Collateral Appraisal System.
    Dot-source this from the Deploy-*.ps1 / Invoke-DbMigrate.ps1 scripts:

        . "$PSScriptRoot/deploy.config.ps1"

.NOTES
    Edit the values below to match THIS server. Each app server can keep its own
    copy if paths differ; otherwise the file is identical across servers.
    Nothing secret belongs here — the DB connection string is passed in at
    runtime (see Invoke-DbMigrate.ps1), never committed.
#>

# --- IIS -------------------------------------------------------------------
# The application pool that hosts the API (No Managed Code, in-process ANCM).
$Global:CasAppPoolName = 'CAS'

# --- Live folders (what IIS actually serves) -------------------------------
$Global:CasApiLivePath = 'C:\inetpub\CAS\api'   # backend physical path
$Global:CasWebLivePath = 'C:\inetpub\CAS\web'   # frontend static files

# --- Staging (temp) + backups ---------------------------------------------
# You copy CAS-<version>.zip here and expand it to C:\Deploy\temp\<version>\.
$Global:CasTempRoot    = 'C:\Deploy\temp'
$Global:CasBackupRoot  = 'C:\Deploy\backups'

# --- Health check ----------------------------------------------------------
# Hit after restart to confirm the node is up. /health/live = process up;
# /health/ready = dependencies ready (the gate the F5 uses for rotation).
$Global:CasHealthUrl   = 'http://localhost:7111/health/ready'

# --- Files that live ONLY on the server and must NEVER be overwritten ------
# robocopy /XF (files) and /XD (dirs) use these. appsettings.Production.json
# is generated on the server from the template; web.config is server-owned.
$Global:CasPreserveFiles = @('appsettings.Production.json', 'web.config', 'app_offline.htm')
$Global:CasPreserveDirs  = @('logs', 'DataProtection-Keys')  # keys are in DB, but harmless to guard
