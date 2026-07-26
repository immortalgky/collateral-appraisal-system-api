#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate the plain-SQL database deployment bundle so a DBA can deploy the
    schema with SSMS / sqlcmd — WITHOUT running Database.exe on the server.

.DESCRIPTION
    Reproduces, as ordinary .sql files, exactly what `Database.exe migrate` does:

      00_Prepare.sql                 dbo.DatabaseMigrationHistory (the DbUp journal)
      01_EF_<nn>_<Context>.sql       EF Core idempotent migration script, one per
                                     DbContext, numbered in the dependency order
                                     used by EfCoreMigrationService
      02_Repeatable_ViewsAndProcs    every Views/ + StoredProcedures/ script,
                                     dependency-ordered, each followed by its
                                     journal upsert (same SHA-256 checksum the
                                     tool computes, so a later tool run is a no-op)
      03_OneTime_DataScripts.sql     every Migration/Scripts/*.sql (seed + data
                                     fixes), each skipped if already journaled
      99_Verify.sql                  read-only post-deployment verification

    Run order is the tool's order: EF tables -> repeatable objects -> one-time
    data scripts. Every file is idempotent and safe to re-run.

.NOTES
    Requires the .NET 9 SDK and dotnet-ef (`dotnet tool install --global dotnet-ef`).
    Generation never touches a database.

.EXAMPLE
    pwsh deploy/New-DbDeploymentScripts.ps1 -Version 20260723-101500
    pwsh deploy/New-DbDeploymentScripts.ps1 -OutDir ./out/db -SkipEf
#>
[CmdletBinding()]
param(
    [string]$Version = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [string]$OutDir,
    [string]$Environment = 'Production',
    [switch]$SkipEf                       # regenerate only the SQL-file sections (fast)
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {

if (-not $OutDir) { $OutDir = Join-Path (Join-Path './dist-artifacts' $Version) 'db' }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

# EF Core contexts in the SAME order as Database/Migration/EfCoreMigrationService.cs
# (Common creates common.RequestStatusSummaries used by a Request migration;
#  Parameter creates tables referenced by Appraisal).
$contexts = @(
    @{ Ctx = 'CommonDbContext';       Project = 'Modules/Common/Common';            Schema = 'common' }
    @{ Ctx = 'ParameterDbContext';    Project = 'Modules/Parameter/Parameter';      Schema = 'dbo' }
    @{ Ctx = 'RequestDbContext';      Project = 'Modules/Request/Request';          Schema = 'request' }
    @{ Ctx = 'WorkflowDbContext';     Project = 'Modules/Workflow/Workflow';        Schema = 'workflow' }
    @{ Ctx = 'DocumentDbContext';     Project = 'Modules/Document/Document';        Schema = 'document' }
    @{ Ctx = 'NotificationDbContext'; Project = 'Modules/Notification/Notification'; Schema = 'notification' }
    @{ Ctx = 'AuthDbContext';         Project = 'Modules/Auth/Auth';                Schema = 'auth' }
    @{ Ctx = 'IntegrationDbContext';  Project = 'Modules/Integration/Integration';  Schema = 'integration' }
    @{ Ctx = 'AppraisalDbContext';    Project = 'Modules/Appraisal/Appraisal';      Schema = 'appraisal' }
    @{ Ctx = 'CollateralDbContext';   Project = 'Modules/Collateral/Collateral';    Schema = 'collateral' }
    @{ Ctx = 'ReportingDbContext';    Project = 'Modules/Reporting/Reporting.Data'; Schema = 'reporting' }
)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-Sql {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

# The tool hashes the script text (BOM stripped by StreamReader) as UTF-8 bytes.
function Get-ScriptChecksum {
    param([string]$Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))) }
    finally { $sha.Dispose() }
}

function Read-ScriptText {
    param([string]$Path)
    $t = [System.IO.File]::ReadAllText($Path)     # detects + strips the BOM, like StreamReader
    return $t
}

# MSBuild embedded-resource name: RootNamespace + relative path, '/' -> '.'
function Get-ResourceName {
    param([string]$FullPath)
    $rel = [System.IO.Path]::GetRelativePath((Join-Path $repoRoot 'Database'), $FullPath)
    return 'Database.' + ($rel -replace '[\\/]', '.')
}

function New-Banner {
    param([string]$Text)
    return @"
/* ===========================================================================
   $Text
   =========================================================================== */
"@
}

$stamp = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
$header = @"
/* ===========================================================================
   Collateral Appraisal System — database deployment
   Release  : $Version
   Generated: $stamp  (deploy/New-DbDeploymentScripts.ps1)
   Target   : SQL Server 2019+   Run with SQLCMD mode / sqlcmd -b
   Every script in this bundle is IDEMPOTENT and safe to re-run.
   =========================================================================== */
"@

# ---------------------------------------------------------------------------
# 00 — journal table (mirrors MigrationService.EnsureMigrationHistoryTableAsync)
# ---------------------------------------------------------------------------
Write-Host '==> 00_Prepare.sql' -ForegroundColor Cyan
$prepare = @"
$header
$(New-Banner '00 — Migration journal table (dbo.DatabaseMigrationHistory)')
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = 'DatabaseMigrationHistory' AND type = 'U')
BEGIN
    CREATE TABLE dbo.DatabaseMigrationHistory (
        Id              int IDENTITY(1,1) PRIMARY KEY,
        ScriptName      nvarchar(255) NOT NULL,
        Applied         datetime2     NOT NULL DEFAULT GETDATE(),
        ScriptChecksum  nvarchar(64)  NULL,
        ExecutedOn      datetime2     NOT NULL DEFAULT GETDATE(),
        ExecutedBy      nvarchar(100) NOT NULL DEFAULT SYSTEM_USER,
        ExecutionTimeMs int           NULL,
        Environment     nvarchar(50)  NULL,
        Version         nvarchar(50)  NULL,
        Success         bit           NULL,
        ErrorMessage    nvarchar(max) NULL
    );
    CREATE INDEX IX_DatabaseMigrationHistory_ScriptName ON dbo.DatabaseMigrationHistory(ScriptName);
    CREATE INDEX IX_DatabaseMigrationHistory_Applied    ON dbo.DatabaseMigrationHistory(Applied);
    PRINT 'Created dbo.DatabaseMigrationHistory.';
END
ELSE
    PRINT 'dbo.DatabaseMigrationHistory already exists — nothing to do.';
GO
"@
Write-Sql (Join-Path $OutDir '00_Prepare.sql') $prepare

# ---------------------------------------------------------------------------
# 01 — EF Core idempotent migration scripts
# ---------------------------------------------------------------------------
if ($SkipEf) {
    Write-Host '==> 01_EF_*.sql skipped (-SkipEf)' -ForegroundColor Yellow
} else {
    $i = 0
    foreach ($c in $contexts) {
        $i++
        $n    = '{0:d2}' -f $i
        $file = Join-Path $OutDir "01_EF_${n}_$($c.Ctx).sql"
        Write-Host "==> 01_EF_${n}_$($c.Ctx).sql" -ForegroundColor Cyan
        # No --no-build: a stale assembly silently produces the wrong script.
        dotnet ef migrations script --idempotent `
            --context $c.Ctx `
            --project $c.Project `
            --startup-project 'Bootstrapper/Api' `
            --output $file
        if ($LASTEXITCODE -ne 0) { throw "dotnet ef failed for $($c.Ctx)" }
        if (-not (Test-Path $file)) { throw "dotnet ef produced no output for $($c.Ctx)" }

        # Prepend a banner (EF writes a BOM'd file; rewrite it without one).
        $body = Read-ScriptText $file
        Write-Sql $file (@"
$header
$(New-Banner "01.$n — EF Core migrations: $($c.Ctx)   (history table: [$($c.Schema)].[__EFMigrationsHistory])")
$body
"@)
    }
}

# ---------------------------------------------------------------------------
# 02 — repeatable objects (views / stored procedures / functions)
# ---------------------------------------------------------------------------
Write-Host '==> 02_Repeatable_ViewsAndProcs.sql' -ForegroundColor Cyan

$repeatDirs = @('Database/Scripts/Views', 'Database/Scripts/StoredProcedures', 'Database/Scripts/Functions') |
    Where-Object { Test-Path $_ }
$repeatFiles = @(Get-ChildItem -Path $repeatDirs -Recurse -Filter *.sql -File | Sort-Object FullName)
if ($repeatFiles.Count -eq 0) { throw 'No repeatable scripts found — is the working directory correct?' }

$items = foreach ($f in $repeatFiles) {
    $text = Read-ScriptText $f.FullName
    [pscustomobject]@{
        Name     = $f.BaseName                       # vw_TaskList / sp_GetTaskList
        Resource = Get-ResourceName $f.FullName
        Text     = $text
        Checksum = Get-ScriptChecksum $text
    }
}

# Dependency order: a view that SELECTs from another view must be created after it.
# (Database/Migration/DatabaseMigrator.cs solves this at runtime by retrying on SQL
#  error 208; offline we resolve it statically instead.)
$byName = @{}; foreach ($it in $items) { $byName[$it.Name] = $it }
$deps = @{}
foreach ($it in $items) {
    # Analyse the code only: these files carry long header comments that name sibling
    # views, which would otherwise register as false dependencies (and as cycles).
    $code = [regex]::Replace($it.Text, '/\*.*?\*/', '', 'Singleline')
    $code = [regex]::Replace($code, '--[^\r\n]*', '')
    $set = New-Object System.Collections.Generic.HashSet[string]
    foreach ($other in $items) {
        if ($other.Name -eq $it.Name) { continue }
        if ($code -match "(?<![\w\[])$([regex]::Escape($other.Name))(?![\w\]])") { [void]$set.Add($other.Name) }
    }
    $deps[$it.Name] = $set
}
$ordered = New-Object System.Collections.Generic.List[object]
$emitted = New-Object System.Collections.Generic.HashSet[string]
$remaining = [System.Collections.Generic.List[object]]::new()
foreach ($it in ($items | Sort-Object Name)) { $remaining.Add($it) }
while ($remaining.Count -gt 0) {
    $ready = @($remaining | Where-Object {
        $unmet = @($deps[$_.Name] | Where-Object { $byName.ContainsKey($_) -and -not $emitted.Contains($_) })
        $unmet.Count -eq 0
    })
    if ($ready.Count -eq 0) {
        # Circular / self-referencing set — emit the rest alphabetically and warn.
        Write-Warning ("Could not fully order: {0}. Emitting alphabetically." -f (($remaining | ForEach-Object Name) -join ', '))
        foreach ($r in $remaining) { $ordered.Add($r) }
        break
    }
    foreach ($r in $ready) { $ordered.Add($r); [void]$emitted.Add($r.Name); [void]$remaining.Remove($r) }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine($header)
[void]$sb.AppendLine((New-Banner "02 — Repeatable objects: $($ordered.Count) views / stored procedures, dependency-ordered.
   Each is CREATE OR ALTER, so re-running is safe and always leaves the object current."))
[void]$sb.AppendLine('SET NOCOUNT ON;')
[void]$sb.AppendLine('GO')
[void]$sb.AppendLine()
foreach ($it in $ordered) {
    [void]$sb.AppendLine("-- ---------------------------------------------------------------------------")
    [void]$sb.AppendLine("-- $($it.Name)")
    [void]$sb.AppendLine("-- ---------------------------------------------------------------------------")
    [void]$sb.AppendLine("PRINT '  $($it.Name)';")
    [void]$sb.AppendLine('GO')
    [void]$sb.AppendLine($it.Text.TrimEnd())
    [void]$sb.AppendLine('GO')
    # Journal upsert with the tool's own checksum, so a later Database.exe run skips it.
    $res = $it.Resource.Replace("'", "''")
    $chk = $it.Checksum.Replace("'", "''")
    [void]$sb.AppendLine(@"
MERGE dbo.DatabaseMigrationHistory AS target
USING (SELECT N'$res' AS ScriptName, N'$chk' AS ScriptChecksum) AS source
   ON target.ScriptName = source.ScriptName
WHEN MATCHED THEN UPDATE SET
     ScriptChecksum = source.ScriptChecksum, Applied = GETDATE(), ExecutedOn = GETDATE(),
     ExecutedBy = SYSTEM_USER, Environment = N'$Environment', Version = N'$Version', Success = 1
WHEN NOT MATCHED THEN INSERT
     (ScriptName, Applied, ScriptChecksum, ExecutedOn, ExecutedBy, Environment, Version, Success)
     VALUES (source.ScriptName, GETDATE(), source.ScriptChecksum, GETDATE(), SYSTEM_USER, N'$Environment', N'$Version', 1);
GO

"@)
}
Write-Sql (Join-Path $OutDir '02_Repeatable_ViewsAndProcs.sql') $sb.ToString()

# ---------------------------------------------------------------------------
# 03 — one-time data / seed scripts (DbUp journaled)
# ---------------------------------------------------------------------------
Write-Host '==> 03_OneTime_DataScripts.sql' -ForegroundColor Cyan
$oneTime = @(Get-ChildItem -Path 'Database/Migration/Scripts' -Recurse -Filter *.sql -File | Sort-Object Name)
if ($oneTime.Count -eq 0) { throw 'No one-time scripts found under Database/Migration/Scripts.' }

$sb2 = [System.Text.StringBuilder]::new()
[void]$sb2.AppendLine($header)
[void]$sb2.AppendLine((New-Banner "03 — One-time data scripts: $($oneTime.Count) files, in name order.
   Each block is skipped (SET NOEXEC ON) when dbo.DatabaseMigrationHistory already
   records it — exactly the check DbUp performs — so re-running the file is safe."))
[void]$sb2.AppendLine('SET NOCOUNT ON;')
[void]$sb2.AppendLine('GO')
[void]$sb2.AppendLine()

foreach ($f in $oneTime) {
    # Environment-scoped scripts (name contains ".env.<environment>.") — same filter
    # as DatabaseMigrator.FilterScriptsByEnvironment.
    if ($f.Name -match '\.env\.' -and $f.Name -notmatch "\.env\.$($Environment.ToLower())\.") {
        Write-Host "    skipping $($f.Name) (other environment)" -ForegroundColor DarkGray
        continue
    }
    $text = (Read-ScriptText $f.FullName).TrimEnd()
    $res  = (Get-ResourceName $f.FullName).Replace("'", "''")
    [void]$sb2.AppendLine("-- ---------------------------------------------------------------------------")
    [void]$sb2.AppendLine("-- $($f.Name)")
    [void]$sb2.AppendLine("-- ---------------------------------------------------------------------------")
    [void]$sb2.AppendLine(@"
IF EXISTS (SELECT 1 FROM dbo.DatabaseMigrationHistory WHERE ScriptName = N'$res')
BEGIN
    PRINT '  SKIP (already applied): $($f.Name)';
    SET NOEXEC ON;
END
ELSE
    PRINT '  APPLY: $($f.Name)';
GO
"@)
    [void]$sb2.AppendLine($text)
    [void]$sb2.AppendLine('GO')
    [void]$sb2.AppendLine(@"
SET NOEXEC OFF;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.DatabaseMigrationHistory WHERE ScriptName = N'$res')
    INSERT INTO dbo.DatabaseMigrationHistory
        (ScriptName, Applied, ExecutedOn, ExecutedBy, Environment, Version, Success)
    VALUES (N'$res', GETDATE(), GETDATE(), SYSTEM_USER, N'$Environment', N'$Version', 1);
GO

"@)
}
Write-Sql (Join-Path $OutDir '03_OneTime_DataScripts.sql') $sb2.ToString()

# ---------------------------------------------------------------------------
# 99 — verification (read-only)
# ---------------------------------------------------------------------------
Write-Host '==> 99_Verify.sql' -ForegroundColor Cyan
$expectedRepeatable = $ordered.Count
$expectedOneTime    = $oneTime.Count
$expectedSchemas = (($contexts | ForEach-Object { "'$($_.Schema)'" }) | Sort-Object -Unique) -join ', '

$verify = @"
$header
$(New-Banner '99 — Post-deployment verification (READ ONLY — safe on production)')
SET NOCOUNT ON;
GO

PRINT '--- 1. EF Core migrations applied, per __EFMigrationsHistory table -----';
-- Discovered dynamically rather than hard-coded: ParameterDbContext creates its
-- history table UNQUALIFIED, so it lands in the executing login's DEFAULT SCHEMA.
-- Expect one row per module schema: $expectedSchemas
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql
     + CASE WHEN @sql = N'' THEN N'' ELSE N' UNION ALL ' END
     + N'SELECT ' + QUOTENAME(s.name, '''') + N' AS [Schema], COUNT(*) AS AppliedMigrations FROM '
     + QUOTENAME(s.name) + N'.[__EFMigrationsHistory]'
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name = N'__EFMigrationsHistory';
IF @sql = N''
    PRINT '  *** NO __EFMigrationsHistory TABLE FOUND — the 01_EF_* scripts did not run. ***';
ELSE
    EXEC sp_executesql (@sql + N' ORDER BY [Schema];');
GO

PRINT '--- 1b. Stray history tables (expect none outside the module schemas) --';
SELECT s.name AS [UnexpectedSchema]
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name = N'__EFMigrationsHistory'
  AND s.name NOT IN ($expectedSchemas);
GO

PRINT '--- 2. Journal totals (expect repeatable >= $expectedRepeatable, one-time >= $expectedOneTime) ---';
SELECT
    SUM(CASE WHEN ScriptName LIKE 'Database.Scripts.%'           THEN 1 ELSE 0 END) AS RepeatableObjects,
    SUM(CASE WHEN ScriptName LIKE 'Database.Migration.Scripts.%' THEN 1 ELSE 0 END) AS OneTimeScripts,
    MAX(Applied) AS LastApplied
FROM dbo.DatabaseMigrationHistory;
GO

PRINT '--- 3. Objects actually present in the database ------------------------';
SELECT type_desc, COUNT(*) AS [Count]
FROM sys.objects
WHERE type IN ('V','P','FN','IF','TF')
GROUP BY type_desc
ORDER BY type_desc;
GO

PRINT '--- 4. Unresolved references in views / procedures (expect 0 rows) -----';
SELECT DISTINCT
       OBJECT_SCHEMA_NAME(d.referencing_id) AS [Schema],
       OBJECT_NAME(d.referencing_id)        AS [Object],
       d.referenced_entity_name             AS [MissingReference]
FROM sys.sql_expression_dependencies d
WHERE d.referenced_id IS NULL
  AND d.is_ambiguous = 0
  AND d.referenced_entity_name NOT LIKE '#%'   -- temp tables are resolved at run time
ORDER BY [Schema], [Object];
GO

PRINT '--- 5. Reference data spot-check ---------------------------------------';
SELECT 'DopaProvinces'   AS [Table], COUNT(*) AS Rows FROM parameter.DopaProvinces
UNION ALL SELECT 'DopaDistricts',    COUNT(*) FROM parameter.DopaDistricts
UNION ALL SELECT 'DopaSubDistricts', COUNT(*) FROM parameter.DopaSubDistricts;
GO

PRINT '--- 6. Release stamp ---------------------------------------------------';
SELECT TOP 20 ScriptName, Version, Environment, Applied, ExecutedBy
FROM dbo.DatabaseMigrationHistory
ORDER BY Applied DESC;
GO
"@
Write-Sql (Join-Path $OutDir '99_Verify.sql') $verify

# ---------------------------------------------------------------------------
Write-Host ''
Write-Host "Database deployment scripts written to $OutDir" -ForegroundColor Green
Get-ChildItem $OutDir -Filter *.sql | Sort-Object Name |
    ForEach-Object { '  {0,-40} {1,8:N0} KB' -f $_.Name, ($_.Length / 1KB) }

}
finally { Pop-Location }
