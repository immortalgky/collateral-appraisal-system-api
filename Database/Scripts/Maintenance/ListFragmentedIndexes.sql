/*
=============================================================================
 ListFragmentedIndexes.sql
-----------------------------------------------------------------------------
 Lists indexes in the CURRENT database ordered by fragmentation (worst first).

 DIAGNOSTIC ONLY - this script is NOT run by DbUp (it lives under
 Scripts\Maintenance\, not Scripts\Views|StoredProcedures|Functions\).
 Run it manually against the CollateralAppraisal database.

 Notes:
   * 'LIMITED' scan mode is the lightest/fastest and is enough for the
     fragmentation %. avg_page_space_used_in_percent may be NULL in this mode;
     switch to 'SAMPLED' or 'DETAILED' if you need that column to be accurate.
   * page_count >= 100 filter ignores tiny indexes where fragmentation is noise.
   * Thresholds follow Microsoft guidance: 5-30% -> REORGANIZE, >=30% -> REBUILD.
=============================================================================
*/

SELECT
    DB_NAME()                                                AS DatabaseName,
    SCHEMA_NAME(t.schema_id)                                 AS SchemaName,
    t.name                                                   AS TableName,
    i.name                                                   AS IndexName,
    i.type_desc                                              AS IndexType,
    ips.index_id,
    ips.partition_number,
    ips.page_count,
    ips.record_count,
    CAST(ips.avg_fragmentation_in_percent AS DECIMAL(5, 2))  AS AvgFragmentationPct,
    CAST(ips.avg_page_space_used_in_percent AS DECIMAL(5, 2)) AS AvgPageSpaceUsedPct,
    CASE
        WHEN ips.avg_fragmentation_in_percent >= 30 THEN 'REBUILD'
        WHEN ips.avg_fragmentation_in_percent >= 5  THEN 'REORGANIZE'
        ELSE 'OK'
    END                                                      AS Recommendation
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
JOIN sys.tables  t ON t.object_id = ips.object_id
JOIN sys.indexes i ON i.object_id = ips.object_id
                  AND i.index_id  = ips.index_id
WHERE ips.index_id > 0          -- exclude heaps
  AND i.name IS NOT NULL        -- named indexes only
  AND ips.page_count >= 100     -- ignore tiny indexes
  -- AND SCHEMA_NAME(t.schema_id) = 'appraisal'   -- uncomment to scope to one module
ORDER BY ips.avg_fragmentation_in_percent DESC;
