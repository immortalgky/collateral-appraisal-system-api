-- ============================================================
-- AS400 interface jobs — schedule, explicit time zone, and archiving behaviour
-- Schemas: integration
--
-- THREE CHANGES, all driven by facts about the feeds that the original schedule got wrong.
--
-- 1. WHEN THE INGEST JOBS RUN.
--    Both AS400 files are produced after midnight. The COLLATLINK job was set to 22:00, which meant
--    it picked up a file that had been sitting there for ~21 hours and the outbound result then
--    echoed ids nearly a day old. Ingest now runs early morning, and the export follows it.
--
-- 2. WHY INGEST IS DAILY THOUGH THE FILES ARE MONTHLY.
--    COLLATREV was on '0 1 1 * *' — one shot, 01:00 on the 1st. Nothing guarantees delivery before
--    then, and a miss meant the file sat in the inbox for another month with no reappraisal
--    candidates raised. Checking once a day costs a directory listing when there is nothing new
--    (integration.InboundFileLogs is what stops a file being ingested twice, so re-listing an old
--    file is harmless).
--
-- 3. TIME ZONE IS NOW EXPLICIT PER JOB.
--    TimeZoneId was NULL, so every job inherited TimeZone:DefaultTimeZone. That value is
--    'Asia/Bangkok' in appsettings.json but 'UTC' in appsettings.Production.json.template — on a
--    host using the template, '0 5 * * *' would fire at noon Bangkok time and nobody would notice
--    the whole AS400 schedule had shifted. Pinning it here makes the intended wall-clock time part
--    of the schedule rather than a side effect of environment config.
--
-- Cron values are operational settings an admin can retune in integration.JobSchedules without a
-- deploy; this script only sets the starting point. They still need confirming against the hour
-- AS400 actually delivers, and against the deadline for the outbound result.
--
-- Idempotent: only touches rows that exist, and DbUp journals the script once per database.
-- ============================================================

-- Ingest COLLATLINK (monthly file, checked daily).
UPDATE integration.JobSchedules
SET CronExpression = '0 5 * * *',
    TimeZoneId     = 'Asia/Bangkok'
WHERE JobId = 'host-collateral-link-as400';

-- Ingest COLLATREV (monthly file, checked daily). Half an hour after the link job purely to keep the
-- two off the same worker at the same instant; there is no data dependency between them.
UPDATE integration.JobSchedules
SET CronExpression = '30 5 * * *',
    TimeZoneId     = 'Asia/Bangkok'
WHERE JobId = 'reappraisal-as400';

-- Outbound results. Deliberately NOT gated on the link ingest: appraisals finish every day while the
-- link file lands once a month, so blocking the export until a fresh file arrived would hold
-- completed work for weeks. It sends whatever collateral ids are on hand.
UPDATE integration.JobSchedules
SET CronExpression = '0 7 * * *',
    TimeZoneId     = 'Asia/Bangkok'
WHERE JobId = 'collateral-result-export';

-- Remaining jobs keep their schedule but get the same explicit zone, so the whole table reads
-- consistently and no job silently follows a different clock.
UPDATE integration.JobSchedules
SET TimeZoneId = 'Asia/Bangkok'
WHERE TimeZoneId IS NULL;
GO

-- Production drop folders belong to AS400: we read from them and cannot move anything out. The
-- ledger (integration.InboundFileLogs) is what prevents reprocessing now, so archiving is optional —
-- a NULL ProcessedDirectory turns it off. Local and development environments keep their folders and
-- carry on tidying the inbox after each run.
--
-- Left as a documented no-op rather than an UPDATE: whether this environment can move files is an
-- environment fact, not something a migration should decide. Run this on hosts that read a
-- read-only drop folder:
--
--   UPDATE integration.FileInterfaceConfigs
--   SET ProcessedDirectory = NULL
--   WHERE InterfaceCode IN ('HOST_COLLATERAL_LINK', 'REAPPRAISAL');
GO

PRINT 'AS400 job schedules and time zones updated.';
GO
