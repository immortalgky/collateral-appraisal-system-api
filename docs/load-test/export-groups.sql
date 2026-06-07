-- Phase 3 group-list export.
-- Pricing Analysis is created per PropertyGroup. Phase 1 auto-creates one
-- "Group 1" per appraisal, so this emits one groupId per load-test appraisal.
-- Run AFTER the phase-1 drain has completed.
--
-- Marker join goes through request.Requests (Requestor='loadtest'); the
-- Appraisals.CreatedBy column is the consumer/dev user, NOT the marker.
--
-- Dump to CSV (one groupId per line) for fill-pricing.js:
--   docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd \
--     -S localhost -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal \
--     -W -h -1 < docs/load-test/export-groups.sql > docs/load-test/grouplist.csv
--   (-h -1 = no header, -W = trim; SET NOCOUNT ON keeps the CSV clean.)

SET NOCOUNT ON;

SELECT CAST(pg.Id AS char(36)) AS groupId
FROM appraisal.PropertyGroups pg
JOIN appraisal.Appraisals ap
    ON ap.Id = pg.AppraisalId
JOIN request.Requests r
    ON r.Id = ap.RequestId
   AND r.Requestor = 'loadtest'
ORDER BY ap.Id, pg.GroupNumber;
