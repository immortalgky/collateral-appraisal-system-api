-- Phase 2 work-list export.
-- Phase 1 creates the appraisal SKELETON asynchronously. Run this AFTER the
-- phase-1 drain has completed (monitor.sql query 2 == target) so every property
-- row exists, then feed the output to fill-detail.js via a SharedArray.
--
-- Output: one (appraisalId, propertyId, propertyType) row per property under the
-- 'loadtest' marker, for the families create-submit.js generates:
--   'L'  (collateralType 01) -> land-detail
--   'LB' (collateralType 02) -> land-and-building-detail
--   'U'  (collateralType 08) -> condo-detail
-- fill-detail.js branches on propertyType to pick the endpoint + body.
--
-- Dump to CSV (no header, comma-separated) for k6.
--
-- macOS / no local sqlcmd — run it INSIDE the SQL Server container (no install):
--   docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd \
--     -S localhost -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal \
--     -s"," -W -h -1 < docs/load-test/export-worklist.sql > docs/load-test/worklist.csv
--
-- If you DO have sqlcmd locally:
--   sqlcmd -S localhost,1433 -U sa -P 'P@ssw0rd' -d CollateralAppraisal -C \
--          -i docs/load-test/export-worklist.sql -s"," -W -h -1 -o docs/load-test/worklist.csv
--
-- Flags: -h -1 = no header, -W = trim whitespace, -s"," = comma separator,
-- -C = trust the dev self-signed cert (required by mssql-tools18). SET NOCOUNT ON
-- below suppresses the "rows affected" trailer, so the CSV needs no post-editing.

SET NOCOUNT ON;

SELECT
    CAST(ap.Id AS char(36)) AS appraisalId,
    CAST(p.Id  AS char(36)) AS propertyId,
    p.PropertyType          AS propertyType
FROM appraisal.Appraisals ap
JOIN request.Requests r
    ON r.Id = ap.RequestId
   AND r.Requestor = 'loadtest'
JOIN appraisal.AppraisalProperties p
    ON p.AppraisalId = ap.Id
   AND p.PropertyType IN ('L', 'LB', 'U')
ORDER BY ap.Id, p.SequenceNumber;
