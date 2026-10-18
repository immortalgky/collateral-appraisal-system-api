-- ============================================================
-- Data fix: the "use the old file by default" flag of a document type now defaults to OFF.
--
-- parameter.DocumentTypes.CarryForwardByDefault used to default to 1, so every type's files were pushed into
-- a new request that referenced a prior appraisal. The prior's files are now all offered and the maker picks;
-- only the types an admin ticks on the Document Type page are placed automatically. The column default is
-- flipped to 0 by the EF migration DocumentTypeCarryForwardDefaultsToDontUse; this resets the rows that
-- already exist: 0 for every type, then 1 for D013 (deed, by user decision 2026-10-13) and D036 / D042 / D043
-- (read by name: the summary report and the D042/D043 it is re-typed from; for these three the flag is
-- ignored by the query and locked to 1 in the admin UI).
-- Codes are matched trimmed and case-insensitively.
--
-- WARNING: this overwrites any choice an admin already made on a database that has the column. Run once;
-- admins tick more types afterwards. Not an EF migration; no audit columns are touched.
-- Idempotent: a second run changes nothing (only rows whose value would change are updated).
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

UPDATE dt
SET    dt.[CarryForwardByDefault] = x.[Wanted]
FROM   [parameter].[DocumentTypes] dt
       CROSS APPLY (SELECT CASE WHEN UPPER(LTRIM(RTRIM(dt.[Code]))) IN ('D013', 'D036', 'D042', 'D043')
                                THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [Wanted]) x
WHERE  dt.[CarryForwardByDefault] <> x.[Wanted];
