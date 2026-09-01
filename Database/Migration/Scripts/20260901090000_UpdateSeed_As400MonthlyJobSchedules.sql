-- ============================================================
-- AS400 interface jobs — all four move to the 2nd of the month
-- Schema: integration
--
--   02:00  reappraisal-as400            ingest COLLATREV
--   02:00  host-collateral-link-as400   ingest COLLATLINK
--   03:00  regulatory-export            RDTCLSINT4.txt + the Excel companion
--   04:00  collateral-result-export     CAS_APPRE_yyyyMMdd.txt
--
-- The 2nd, not the 1st: AS400 produces these files after its month-end close, so a run on the 1st
-- reads whatever was there from the month before.
--
-- WHY THE HOUR GAPS MATTER. Both exports read what the ingests write — regulatory-export takes its
-- entire row set from collateral.HostCollateralLinks, and collateral-result-export can only emit an
-- appraisal that already has a host link. Nothing enforces the ordering at runtime: an export that
-- starts before the ingest has finished does not fail, it quietly ships last month's picture. If an
-- ingest ever needs more than an hour (a backlog of files, a slow host), move the EXPORTS later
-- rather than pulling the ingests earlier.
--
-- WHAT THIS REPLACES, AND THE TRADE-OFF THAT COMES BACK.
-- 20260831090000_SeedData_As400JobScheduleAndTimeZone.sql had put both ingests on a DAILY cron
-- ('0 5 * * *' and '30 5 * * *') on the reasoning that the files are monthly but nothing guarantees
-- the hour they land, and that a missed one-shot run leaves the file sitting in the inbox for
-- another month. That risk is now back by request: if the file is not there at 02:00 on the 2nd,
-- nothing is ingested until the 2nd of next month, and both exports run on stale links in between.
--
-- integration.InboundFileLogs makes a daily check almost free (one directory listing when there is
-- nothing new), so if a late delivery is ever observed, the fix is to put the two ingests back on a
-- daily cron in the admin screen — the exports can stay monthly. Same reasoning applies to
-- collateral-result-export, which was daily so that finished appraisals reached the host within a
-- day; monthly means work completed on the 3rd waits until the 2nd of the following month.
--
-- These are operational settings. An admin retunes them at /admin/job-schedules without a deploy; this
-- script only moves the starting point, and the matching defaults in IntegrationRecurringJobs.All
-- cover databases that have never seeded these rows.
--
-- Idempotent: absolute values, only touches rows that exist. DbUp journals it once per database.
-- ============================================================

UPDATE integration.JobSchedules
SET CronExpression = '0 2 2 * *',
    TimeZoneId     = 'Asia/Bangkok',
    Description    = 'Ingest AS400 COLLATREV reappraisal files (monthly, 2nd at 02:00).'
WHERE JobId = 'reappraisal-as400';

UPDATE integration.JobSchedules
SET CronExpression = '0 2 2 * *',
    TimeZoneId     = 'Asia/Bangkok',
    Description    = 'Ingest AS400 COLLATLINK host-collateral-id files (monthly, 2nd at 02:00, ahead of both exports).'
WHERE JobId = 'host-collateral-link-as400';

UPDATE integration.JobSchedules
SET CronExpression = '0 3 2 * *',
    TimeZoneId     = 'Asia/Bangkok',
    Description    = 'Full monthly regulatory (Basel/RDT) collateral snapshot (2nd at 03:00, after the COLLATLINK ingest).'
WHERE JobId = 'regulatory-export';

UPDATE integration.JobSchedules
SET CronExpression = '0 4 2 * *',
    TimeZoneId     = 'Asia/Bangkok',
    Description    = 'Ship completed-appraisal prices to the AS400 host (monthly, 2nd at 04:00).'
WHERE JobId = 'collateral-result-export';
GO

-- Hangfire keeps its own copy of the cron in hangfire.Hash and is only re-registered from
-- integration.JobSchedules at application start. Restart the API (or re-save the job in the admin
-- screen) after running this, or the dashboard keeps showing the old next-run time.
PRINT 'AS400 job schedules moved to the 2nd of the month (02:00 / 02:00 / 03:00 / 04:00 Asia/Bangkok).';
GO
