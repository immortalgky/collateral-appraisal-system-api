/*
=============================================================================
 RebuildOrReorganizeIndexes.sql
-----------------------------------------------------------------------------
 Generates (and optionally executes) ALTER INDEX REBUILD/REORGANIZE
 statements for fragmented indexes in the CURRENT database.

 DIAGNOSTIC / MAINTENANCE ONLY - this script is NOT run by DbUp (it lives
 under Scripts\Maintenance\, not Scripts\Views|StoredProcedures|Functions\).
 Run it manually against the CollateralAppraisal database, in a low-traffic
 window.

 HOW TO USE
   1. Dry run first: leave @Execute = 0. Each ALTER INDEX statement is PRINTed
      (with frag % and page count as a trailing comment) for review.
   2. Apply: set @Execute = 1. Statements run one at a time, worst first.
   3. Scope: set @SchemaFilter = 'appraisal' (or request/workflow/reporting)
      to only touch one module's tables.
   4. @Online = 1 requires SQL Server Enterprise (or Azure). On Standard it
      errors; leave it 0 (offline rebuild locks the table for its duration).

 CAUTIONS
   * REBUILD updates statistics with fullscan as a side effect; REORGANIZE
     does NOT - consider a separate UPDATE STATISTICS pass after big reorgs.
   * On the single SQL box, offline rebuilds hold locks and reorganize
     generates heavy log. For a production maintenance job prefer Ola
     Hallengren's IndexOptimize (logging, time limits, lock handling).
     This script is for ad-hoc / diagnostic use.
=============================================================================
*/

SET NOCOUNT ON;

DECLARE @MinPageCount     INT     = 100;   -- skip tiny indexes
DECLARE @ReorgThreshold   INT     = 5;     -- 5-30%  -> REORGANIZE
DECLARE @RebuildThreshold INT     = 30;    -- >=30%  -> REBUILD
DECLARE @Online           BIT     = 0;     -- 1 = ONLINE rebuild (Enterprise/Azure only)
DECLARE @Execute          BIT     = 0;     -- 0 = print only, 1 = actually run
DECLARE @UpdateStats      BIT     = 1;     -- 1 = run sp_updatestats after the pass (only when @Execute = 1)
DECLARE @FillFactor       TINYINT = NULL;  -- NULL = keep each index's stored fill factor (recommended).
                                           -- Set 80-95 to apply to ALL rebuilds in this run. Scope with
                                           -- @SchemaFilter so it doesn't bloat sequential (NEWSEQUENTIALID) indexes.
DECLARE @SchemaFilter     SYSNAME = NULL;  -- e.g. 'appraisal', or NULL for all

DECLARE @sql NVARCHAR(MAX);

IF @FillFactor IS NOT NULL AND (@FillFactor < 1 OR @FillFactor > 100)
BEGIN
    RAISERROR('@FillFactor must be between 1 and 100 (or NULL to keep existing).', 16, 1);
    RETURN;
END

-- @Online and @FillFactor are constant for the whole run, so build the REBUILD
-- "WITH (...)" clause once. STUFF strips the leading ', ' from the option list.
DECLARE @RebuildWith NVARCHAR(200) = STUFF(
    CASE WHEN @Online = 1          THEN N', ONLINE = ON' ELSE N'' END +
    CASE WHEN @FillFactor IS NOT NULL THEN N', FILLFACTOR = ' + CAST(@FillFactor AS NVARCHAR(3)) ELSE N'' END,
    1, 2, N'');
SET @RebuildWith = CASE WHEN @RebuildWith = N'' THEN N'' ELSE N' WITH (' + @RebuildWith + N')' END;

IF OBJECT_ID('tempdb..#Frag') IS NOT NULL DROP TABLE #Frag;

SELECT
    SCHEMA_NAME(t.schema_id)            AS SchemaName,
    t.name                             AS TableName,
    i.name                             AS IndexName,
    ips.avg_fragmentation_in_percent   AS FragPct,
    ips.page_count,
    CASE
        WHEN ips.avg_fragmentation_in_percent >= @RebuildThreshold THEN 'REBUILD'
        ELSE 'REORGANIZE'
    END                                AS Action
INTO #Frag
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
JOIN sys.tables  t ON t.object_id = ips.object_id
JOIN sys.indexes i ON i.object_id = ips.object_id AND i.index_id = ips.index_id
WHERE ips.index_id > 0
  AND i.name IS NOT NULL
  AND ips.page_count >= @MinPageCount
  AND ips.avg_fragmentation_in_percent >= @ReorgThreshold
  AND (@SchemaFilter IS NULL OR SCHEMA_NAME(t.schema_id) = @SchemaFilter);

DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT
        N'ALTER INDEX ' + QUOTENAME(IndexName) +
        N' ON ' + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) +
        CASE Action
            WHEN 'REBUILD' THEN N' REBUILD' + @RebuildWith
            ELSE N' REORGANIZE'
        END + N';' +
        N'  -- ' + Action + N' frag='
            + CAST(CAST(FragPct AS DECIMAL(5, 2)) AS NVARCHAR(10))
            + N'% pages=' + CAST(page_count AS NVARCHAR(20))
    FROM #Frag
    ORDER BY FragPct DESC;

OPEN c;
FETCH NEXT FROM c INTO @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT @sql;

    IF @Execute = 1
    BEGIN
        -- strip the trailing comment before executing
        DECLARE @exec NVARCHAR(MAX) = LEFT(@sql, CHARINDEX(N';', @sql));
        EXEC sp_executesql @exec;
    END

    FETCH NEXT FROM c INTO @sql;
END
CLOSE c;
DEALLOCATE c;

DROP TABLE #Frag;

-- Refresh statistics that REORGANIZE left stale (REBUILD already refreshed its own).
-- sp_updatestats only updates stats with row modifications, so it skips the freshly
-- rebuilt ones. Runs only on a real apply pass (@Execute = 1), not on a dry run.
IF @Execute = 1 AND @UpdateStats = 1
BEGIN
    PRINT '-- Running sp_updatestats...';
    EXEC sp_updatestats;
END
