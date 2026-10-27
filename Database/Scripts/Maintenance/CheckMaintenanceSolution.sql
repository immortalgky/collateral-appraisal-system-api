/*
=============================================================================
 CheckMaintenanceSolution.sql
-----------------------------------------------------------------------------
 OPTIONAL, READ-ONLY pre-check for a server that may already have Ola's
 Maintenance Solution or other maintenance jobs, to run BEFORE
 CreateMaintenanceAgentJobs.sql (which installs Ola itself on a fresh server). It changes
 nothing: no objects, no jobs, no data.

 MAINTENANCE ONLY - this script is NOT run by DbUp (it lives under
 Scripts\Maintenance\). Run it in SSMS as a member of sysadmin.

 RESULT SETS
   1. Instance          version, edition, whether SQL Server Agent is running
   2. Ola installation  every database that holds Ola Hallengren's procedures,
                        with the version stamp from the procedure source
                        ('--// Version: yyyy-mm-dd hh:mi:ss'). Use that database
                        as @OlaDatabase. No rows = Ola is not installed yet.
   3. Existing jobs     Agent jobs whose steps call IndexOptimize or
                        DatabaseIntegrityCheck, and whether they would also
                        cover @DatabaseName (USER_DATABASES, ALL_DATABASES, or
                        the name itself). Those would maintain our database
                        twice: exclude it there ('USER_DATABASES, -<db>') or do
                        not run CreateMaintenanceAgentJobs.sql.
   4. Maintenance plans classic SSMS maintenance plans (rebuild/reorganize/
                        statistics tasks may cover the database too - check)
   5. CAS jobs          jobs this project already created (names start 'CAS - ')

 CreateMaintenanceAgentJobs.sql never upgrades or downgrades an existing Ola
 (it stops if the version differs): other applications' jobs on the instance may
 depend on the installed version.
=============================================================================
*/

SET NOCOUNT ON;

DECLARE @DatabaseName sysname = N'CollateralAppraisal';   -- same value as in CreateMaintenanceAgentJobs.sql

------------------------------------------------------------------------------
-- 1. Instance
------------------------------------------------------------------------------
SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) AS ProductVersion,
       CAST(SERVERPROPERTY('Edition') AS nvarchar(100))       AS Edition,
       CASE WHEN EXISTS (SELECT 1 FROM sys.dm_exec_sessions WHERE program_name LIKE N'SQLAgent%')
            THEN 'RUNNING' ELSE 'NOT RUNNING (or no permission to see it)' END AS SqlServerAgent,
       CASE WHEN DB_ID(@DatabaseName) IS NULL THEN 'NOT FOUND' ELSE 'found' END AS TargetDatabase;

------------------------------------------------------------------------------
-- 2. Where is Ola installed?
------------------------------------------------------------------------------
CREATE TABLE #Ola (DatabaseName sysname, ProcedureName sysname, Version nvarchar(30) NULL);

DECLARE @db  sysname = N'';
DECLARE @sql nvarchar(max);
WHILE 1 = 1
BEGIN
    SELECT TOP (1) @db = name FROM sys.databases
    WHERE name > @db AND state = 0 AND HAS_DBACCESS(name) = 1
    ORDER BY name;
    IF @@ROWCOUNT = 0 BREAK;

    -- name comes from sys.databases and is bracket-quoted; no user input reaches this statement
    SET @sql = N'SELECT ' + QUOTENAME(@db, '''') + N', o.name, '
             + N'CASE WHEN CHARINDEX(''--// Version: '', m.definition) > 0 '
             + N'THEN SUBSTRING(m.definition, CHARINDEX(''--// Version: '', m.definition) + 14, 19) END '
             + N'FROM ' + QUOTENAME(@db) + N'.sys.objects o '
             + N'JOIN ' + QUOTENAME(@db) + N'.sys.sql_modules m ON m.object_id = o.object_id '
             + N'WHERE o.type = ''P'' AND o.schema_id = SCHEMA_ID(N''dbo'') '
             + N'AND o.name IN (N''IndexOptimize'', N''DatabaseIntegrityCheck'', N''DatabaseBackup'', N''CommandExecute'')';
    INSERT INTO #Ola EXECUTE (@sql);
END

-- dbo.CommandLog must sit next to the procedures (@LogToTable = 'Y')
CREATE TABLE #Log (DatabaseName sysname);
SET @db = N'';
WHILE 1 = 1
BEGIN
    SELECT TOP (1) @db = DatabaseName FROM (SELECT DISTINCT DatabaseName FROM #Ola) x WHERE DatabaseName > @db ORDER BY DatabaseName;
    IF @@ROWCOUNT = 0 BREAK;
    SET @sql = N'SELECT ' + QUOTENAME(@db, '''') + N' WHERE OBJECT_ID(' + QUOTENAME(QUOTENAME(@db) + N'.dbo.CommandLog', '''') + N', N''U'') IS NOT NULL';
    INSERT INTO #Log EXECUTE (@sql);
END

SELECT o.DatabaseName,
       MAX(CASE WHEN o.ProcedureName = 'IndexOptimize'          THEN ISNULL(o.Version, 'unknown') END) AS IndexOptimize,
       MAX(CASE WHEN o.ProcedureName = 'DatabaseIntegrityCheck' THEN ISNULL(o.Version, 'unknown') END) AS DatabaseIntegrityCheck,
       MAX(CASE WHEN o.ProcedureName = 'CommandExecute'         THEN ISNULL(o.Version, 'unknown') END) AS CommandExecute,
       CASE WHEN COUNT(CASE WHEN o.ProcedureName IN ('IndexOptimize', 'DatabaseIntegrityCheck', 'CommandExecute') THEN 1 END) = 3
             AND EXISTS (SELECT 1 FROM #Log l WHERE l.DatabaseName = o.DatabaseName)
            THEN 'complete' ELSE 'INCOMPLETE (missing procedure or dbo.CommandLog)' END AS Status
FROM #Ola o
GROUP BY o.DatabaseName
ORDER BY o.DatabaseName;

------------------------------------------------------------------------------
-- 3. Existing Agent jobs that call Ola's maintenance procedures
------------------------------------------------------------------------------
SELECT j.name                                    AS JobName,
       j.enabled                                 AS JobEnabled,
       s.step_id                                 AS StepId,
       CASE
           WHEN CHARINDEX(N'-' + @DatabaseName, s.command) > 0
             OR CHARINDEX(N'-[' + @DatabaseName + N']', s.command) > 0 THEN 'EXCLUDES our database (verify)'
           WHEN CHARINDEX(N'USER_DATABASES', s.command) > 0 THEN 'COVERS our database (USER_DATABASES)'
           WHEN CHARINDEX(N'ALL_DATABASES', s.command)  > 0 THEN 'COVERS our database (ALL_DATABASES)'
           WHEN CHARINDEX(@DatabaseName, s.command)     > 0 THEN 'COVERS our database (named)'
           ELSE 'other databases only' END       AS Coverage,
       s.database_name                           AS StepDatabase,
       LEFT(REPLACE(REPLACE(s.command, CHAR(13), N' '), CHAR(10), N' '), 300) AS CommandStart,
       (SELECT COUNT(*) FROM msdb.dbo.sysjobschedules js WHERE js.job_id = j.job_id) AS ScheduleCount
FROM msdb.dbo.sysjobs j
JOIN msdb.dbo.sysjobsteps s ON s.job_id = j.job_id
WHERE (s.command LIKE N'%IndexOptimize%' OR s.command LIKE N'%DatabaseIntegrityCheck%')
  AND j.name NOT LIKE N'CAS - %'   -- ours, listed in result set 5
ORDER BY j.name, s.step_id;

------------------------------------------------------------------------------
-- 4. Classic maintenance plans
------------------------------------------------------------------------------
IF OBJECT_ID(N'msdb.dbo.sysmaintplan_plans', N'U') IS NOT NULL
    SELECT p.name AS MaintenancePlan FROM msdb.dbo.sysmaintplan_plans p ORDER BY p.name;
ELSE
    SELECT CAST(NULL AS sysname) AS MaintenancePlan WHERE 1 = 0;

------------------------------------------------------------------------------
-- 5. Jobs this project already created
------------------------------------------------------------------------------
SELECT j.name AS JobName, j.enabled AS JobEnabled, SUSER_SNAME(j.owner_sid) AS Owner
FROM msdb.dbo.sysjobs j
WHERE j.name LIKE N'CAS - %'
ORDER BY j.name;

DROP TABLE #Ola;
DROP TABLE #Log;
