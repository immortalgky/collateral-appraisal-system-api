-- The newest COLLATREV file ingested — one row, one date. The single definition of "AS400's latest file",
-- read by collateral.vw_ReappraisalCandidates (IsInLatestFile) and the detail page's nearby query. A
-- Pending/Deleted book last seen in an older file is no longer on AS400's due list.
--
-- Read from the file ledger, not only from the rows: Consumed books are skipped without being stamped,
-- so a file that lists only reviewed books would otherwise never register as the latest and dropped
-- books would stay listed. The rows' own maximum still counts, for files ingested before the ledger.
-- reporting.vw_RCAS002_ReappraisalDue keeps its own copy: report views read base tables only.
CREATE OR ALTER VIEW collateral.vw_ReappraisalLatestFile
AS
SELECT CASE WHEN c.FileDate IS NULL OR f.FileDate > c.FileDate THEN f.FileDate ELSE c.FileDate END AS FileDate
FROM (SELECT MAX(l.FileDate) AS FileDate
      FROM integration.InboundFileLogs l
      WHERE l.InterfaceCode = 'REAPPRAISAL' AND l.Status = 'Succeeded') f
CROSS JOIN (SELECT MAX(LastSeenFileDate) AS FileDate
            FROM collateral.ReappraisalCandidates) c;
