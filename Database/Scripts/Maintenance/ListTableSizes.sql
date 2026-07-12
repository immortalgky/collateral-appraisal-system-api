/*
=============================================================================
 ListTableSizes.sql
-----------------------------------------------------------------------------
 Lists tables in the CURRENT database ordered by total space (largest first).

 DIAGNOSTIC ONLY - this script is NOT run by DbUp (it lives under
 Scripts\Maintenance\, not Scripts\Views|StoredProcedures|Functions\).
 Run it manually against the CollateralAppraisal database.

 Notes:
   * Uses sys.dm_db_partition_stats (avoids the deprecated data_pages column).
   * index_id <= 1 counts the heap (0) or clustered index (1) once per table,
     so RowCount is not multiplied by the number of nonclustered indexes.
     reserved / used / in_row page counts still cover ALL indexes on the table.
   * Sizes are MB. Divide by 1024 again for GB.
=============================================================================
*/

SELECT
    DB_NAME()                                                          AS DatabaseName,
    s.name                                                            AS SchemaName,
    t.name                                                            AS TableName,
    SUM(CASE WHEN ps.index_id <= 1 THEN ps.row_count ELSE 0 END)      AS [RowCount],
    CAST(SUM(ps.reserved_page_count) * 8.0 / 1024 AS DECIMAL(18, 2))  AS TotalSpaceMB,
    CAST(SUM(ps.used_page_count) * 8.0 / 1024 AS DECIMAL(18, 2))      AS UsedSpaceMB,
    CAST(SUM(ps.in_row_data_page_count) * 8.0 / 1024 AS DECIMAL(18, 2)) AS DataSpaceMB,
    CAST((SUM(ps.used_page_count) - SUM(ps.in_row_data_page_count))
         * 8.0 / 1024 AS DECIMAL(18, 2))                             AS IndexSpaceMB
FROM sys.dm_db_partition_stats ps
JOIN sys.tables  t ON t.object_id = ps.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0        -- user tables only
  -- AND s.name = 'appraisal'    -- uncomment to scope to one module
GROUP BY s.name, t.name
ORDER BY TotalSpaceMB DESC;
