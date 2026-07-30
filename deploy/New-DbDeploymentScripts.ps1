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
    [switch]$SkipEf,                      # regenerate only the SQL-file sections (fast)

    # Defaults baked into 00_CreateDatabase.sql's DECLARE block. The DBA can edit
    # them in the generated file; these only set the starting point.
    [string]$Collation   = 'SQL_Latin1_General_CP1_CI_AS',
    [int]$DataSizeMB     = 4096,
    [int]$DataGrowthMB   = 512,
    [int]$LogSizeMB      = 2048,
    [int]$LogGrowthMB    = 256
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
   Target   : SQL Server 2019+   Plain T-SQL - runs in SSMS or sqlcmd -b
   Every script in this bundle is IDEMPOTENT and safe to re-run.
   =========================================================================== */
"@

# ---------------------------------------------------------------------------
# 00 — create the database (runs against [master], NOT the target database)
#
# The app no longer creates it: EF's Database.Migrate() used to do so implicitly,
# but startup migration was removed, and every other script in this bundle runs
# *inside* the database so none of them can create it.
#
# Uses the instance default data/log paths (SQL Server picks them from
# InstanceDefaultDataPath / InstanceDefaultLogPath), then resizes the files
# explicitly — production wants fixed-MB autogrowth, not the small defaults.
# ---------------------------------------------------------------------------
Write-Host '==> 00_CreateDatabase.sql' -ForegroundColor Cyan

$createDbEditBlock = @"
    ------------------------------------------------------------------------
    -- EDIT THIS BLOCK, THEN RUN THE WHOLE FILE.
    ------------------------------------------------------------------------
    -- The database to create. If you change it, pass the same name to
    -- Invoke-SqlDeploy.ps1 -Database (that script verifies the two agree).
    DECLARE @DbName     sysname = N'CollateralAppraisal';

    -- MUST match UAT. Nothing in the application pins a collation, so whatever
    -- is set here is what production gets. Thai text is nvarchar, so this
    -- governs sorting and comparison rather than storage -- but a database that
    -- differs from UAT sorts and compares differently, silently.
    DECLARE @Collation  sysname = N'$Collation';

    -- Size up front so autogrowth never fires in normal operation.
    -- Fixed MB, never percent.
    DECLARE @DataSizeMB int = $DataSizeMB;
    DECLARE @DataGrowMB int = $DataGrowthMB;
    DECLARE @LogSizeMB  int = $LogSizeMB;
    DECLARE @LogGrowMB  int = $LogGrowthMB;
    ------------------------------------------------------------------------
    -- END EDIT BLOCK
    ------------------------------------------------------------------------
"@

# Single-quoted here-string: no PowerShell interpolation inside the T-SQL.
$createDbBody = @'

    SET NOCOUNT ON;

    DECLARE @db  nvarchar(300) = QUOTENAME(@DbName);
    DECLARE @sql nvarchar(max);

    PRINT '=== Target database: ' + @DbName + ' ===';

    -- Catch a mistyped collation here rather than via a cryptic CREATE failure.
    IF NOT EXISTS (SELECT 1 FROM sys.fn_helpcollations() WHERE name = @Collation)
    BEGIN
        RAISERROR('Unknown collation "%s" - fix the EDIT block above.', 16, 1, @Collation);
        RETURN;
    END

    /*------------------------------------------------------------------------
      1. Create the database on the instance default data/log paths
    ------------------------------------------------------------------------*/
    IF DB_ID(@DbName) IS NULL
    BEGIN
        SET @sql = N'CREATE DATABASE ' + @db + N' COLLATE ' + @Collation + N';';
        EXEC sys.sp_executesql @sql;
        PRINT '  created (instance default data/log paths).';
    END
    ELSE
        PRINT '  already exists - skipping CREATE; options below still applied.';

    /*------------------------------------------------------------------------
      2. File size and autogrowth

      Logical file names are looked up rather than assumed, so this also works
      on a database created by someone else. SIZE can only grow a file, so it is
      applied only when the file is currently smaller - that keeps re-runs safe.
    ------------------------------------------------------------------------*/
    DECLARE @dataFile sysname, @logFile sysname, @dataMB int, @logMB int;

    SELECT @dataFile = name, @dataMB = size / 128
    FROM sys.master_files
    WHERE database_id = DB_ID(@DbName) AND type = 0 AND file_id = 1;

    SELECT TOP (1) @logFile = name, @logMB = size / 128
    FROM sys.master_files
    WHERE database_id = DB_ID(@DbName) AND type = 1
    ORDER BY file_id;

    IF @dataFile IS NULL OR @logFile IS NULL
    BEGIN
        RAISERROR('Could not resolve the data/log logical file names.', 16, 1);
        RETURN;
    END

    IF @dataMB < @DataSizeMB
    BEGIN
        SET @sql = N'ALTER DATABASE ' + @db + N' MODIFY FILE (NAME = ' + QUOTENAME(@dataFile)
                 + N', SIZE = ' + CAST(@DataSizeMB AS nvarchar(20)) + N'MB);';
        EXEC sys.sp_executesql @sql;
        PRINT '  data file grown to ' + CAST(@DataSizeMB AS varchar(20)) + ' MB.';
    END
    ELSE
        PRINT '  data file already >= target size - left alone.';

    SET @sql = N'ALTER DATABASE ' + @db + N' MODIFY FILE (NAME = ' + QUOTENAME(@dataFile)
             + N', FILEGROWTH = ' + CAST(@DataGrowMB AS nvarchar(20)) + N'MB);';
    EXEC sys.sp_executesql @sql;

    IF @logMB < @LogSizeMB
    BEGIN
        SET @sql = N'ALTER DATABASE ' + @db + N' MODIFY FILE (NAME = ' + QUOTENAME(@logFile)
                 + N', SIZE = ' + CAST(@LogSizeMB AS nvarchar(20)) + N'MB);';
        EXEC sys.sp_executesql @sql;
        PRINT '  log file grown to ' + CAST(@LogSizeMB AS varchar(20)) + ' MB.';
    END
    ELSE
        PRINT '  log file already >= target size - left alone.';

    SET @sql = N'ALTER DATABASE ' + @db + N' MODIFY FILE (NAME = ' + QUOTENAME(@logFile)
             + N', FILEGROWTH = ' + CAST(@LogGrowMB AS nvarchar(20)) + N'MB);';
    EXEC sys.sp_executesql @sql;

    /*------------------------------------------------------------------------
      3. Recovery model

      FULL is required for point-in-time restore. It also means the log grows
      until a LOG backup truncates it - a transaction-log backup schedule MUST
      be in place before go-live, or the log volume will fill.
    ------------------------------------------------------------------------*/
    SET @sql = N'ALTER DATABASE ' + @db + N' SET RECOVERY FULL;';
    EXEC sys.sp_executesql @sql;

    /*------------------------------------------------------------------------
      4. Read Committed Snapshot Isolation

      Recommended by docs/SQL_Server_Locking_&_Isolation_Reference.md ("Enable
      RCSI for OLTP systems to reduce reader/writer blocking", and again for
      heavy reporting) - this system is both: OLTP writes plus many reporting
      views and Dapper reads over the same tables.

      Explicit lock hints keep working: AppraisalNumberGenerator's
      UPDATE ... WITH (UPDLOCK, ROWLOCK, HOLDLOCK) still serialises, and the
      background-job lease patterns are unaffected. Cost: row versions live in
      tempdb, so size and monitor tempdb accordingly.

      ROLLBACK IMMEDIATE is safe here only because this runs before the
      application is deployed; it terminates open sessions on the database.
    ------------------------------------------------------------------------*/
    SET @sql = N'ALTER DATABASE ' + @db + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;';
    EXEC sys.sp_executesql @sql;

    /*------------------------------------------------------------------------
      5. Standard production options
    ------------------------------------------------------------------------*/
    SET @sql =
          N'ALTER DATABASE ' + @db + N' SET AUTO_SHRINK OFF;'                  -- fragments indexes; space is reused anyway
        + N'ALTER DATABASE ' + @db + N' SET AUTO_CREATE_STATISTICS ON;'        -- plan quality
        + N'ALTER DATABASE ' + @db + N' SET AUTO_UPDATE_STATISTICS ON;'
        + N'ALTER DATABASE ' + @db + N' SET AUTO_UPDATE_STATISTICS_ASYNC ON;'
        + N'ALTER DATABASE ' + @db + N' SET PAGE_VERIFY CHECKSUM;'             -- detect storage corruption on read
        + N'ALTER DATABASE ' + @db + N' SET AUTO_CLOSE OFF;';                  -- keep the database warm
    EXEC sys.sp_executesql @sql;

    -- Query Store: the most useful thing to already have on when a production
    -- query regresses. READ_WRITE so it captures from day one.
    SET @sql = N'ALTER DATABASE ' + @db + N' SET QUERY_STORE = ON;';
    EXEC sys.sp_executesql @sql;

    SET @sql = N'ALTER DATABASE ' + @db + N' SET QUERY_STORE ('
             + N'OPERATION_MODE = READ_WRITE, '
             + N'CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30), '
             + N'DATA_FLUSH_INTERVAL_SECONDS = 900, '
             + N'INTERVAL_LENGTH_MINUTES = 60, '
             + N'MAX_STORAGE_SIZE_MB = 1024, '
             + N'QUERY_CAPTURE_MODE = AUTO, '
             + N'SIZE_BASED_CLEANUP_MODE = AUTO);';
    EXEC sys.sp_executesql @sql;

    PRINT '  production options applied.';

    /*------------------------------------------------------------------------
      6. Report the result - read this before continuing to 00_Prepare.sql
    ------------------------------------------------------------------------*/
    SELECT
        d.name                            AS [Database],
        d.collation_name                  AS [Collation],
        d.recovery_model_desc             AS [Recovery],
        d.is_read_committed_snapshot_on   AS [RCSI],
        d.is_auto_shrink_on               AS [AutoShrink],
        d.page_verify_option_desc         AS [PageVerify],
        d.is_query_store_on               AS [QueryStore],
        d.compatibility_level             AS [CompatLevel]
    FROM sys.databases d
    WHERE d.name = @DbName;

    SELECT
        mf.name                                       AS [LogicalName],
        mf.type_desc                                  AS [FileType],
        mf.physical_name                              AS [Path],
        CAST(mf.size * 8.0 / 1024 AS decimal(18,0))   AS [SizeMB],
        CASE WHEN mf.is_percent_growth = 1
             THEN CAST(mf.growth AS varchar(10)) + ' %'
             ELSE CAST(CAST(mf.growth * 8.0 / 1024 AS decimal(18,0)) AS varchar(20)) + ' MB'
        END                                           AS [Autogrowth]
    FROM sys.master_files mf
    WHERE mf.database_id = DB_ID(@DbName);
END
GO
'@

Write-Sql (Join-Path $OutDir '00_CreateDatabase.sql') (@"
$header
$(New-Banner '00 — Create the database.  RUN THIS AGAINST [master].

   Plain T-SQL: no SQLCMD mode, no :setvar, no sqlcmd required. Open it in SSMS,
   edit the DECLARE block at the top, press Execute. Invoke-SqlDeploy.ps1 also
   runs it automatically, against master, as the first file in the bundle.

   The database is created on the INSTANCE DEFAULT data/log paths; the files are
   then resized explicitly. Idempotent - safe to re-run.')
USE [master];
GO

BEGIN
$createDbEditBlock
$createDbBody
"@)

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
BEGIN
    -- sp_executesql takes a variable or literal, never an expression: passing
    -- (@sql + N'...') is a syntax error, so build the statement first.
    SET @sql = @sql + N' ORDER BY [Schema];';
    EXEC sys.sp_executesql @sql;
END
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
