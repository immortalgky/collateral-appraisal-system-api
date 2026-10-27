# Database maintenance (for the DBA)

Printable version for the bank: [`docs/deployment/database-maintenance-guide.html`](../docs/deployment/database-maintenance-guide.html).

The application does **no** database maintenance of its own. Its Hangfire jobs only purge
application data (logs, outbox rows, report files). Index upkeep, statistics and integrity
checks run as **SQL Server Agent jobs owned by the DBA**, as described below.

Do not grant the application login the rights to do this itself. `DBCC CHECKDB` needs
`db_owner`/`sysadmin`, and `ALTER INDEX` needs `ALTER` on every table.

## 1. Install — one script, run once, by hand

Everything is in **one** script: `Database/Scripts/Maintenance/CreateMaintenanceAgentJobs.sql`. It
installs Ola Hallengren's Maintenance Solution (free, MIT, <https://ola.hallengren.com>; version
2026-09-12 embedded unchanged, into `master`, with no Ola jobs and no backups) and then creates
the four Agent jobs in section 2. The DBA runs it **once, by hand, in SSMS, as sysadmin**. It is not run by
DbUp, `migrate` or the deploy scripts, and only needs running again to change a setting.

**Server preparation (check before running):**

- [ ] **SQL Server Agent** service is running, and its startup type is **Automatic**.
- [ ] The database server **clock and time zone are Thai**: `SELECT SYSDATETIMEOFFSET();` must end in
  `+07:00`. Agent schedules use the server's local time. On a UTC server 22:00 would fire at
  05:00 Thai time, inside the application's 01:00–05:00 window.
- [ ] The application database exists. Optional: a dedicated database for Ola instead of `master`
  (see the script header: change `@OlaDatabase` **and** the `USE [master]` line in Ola's section).

**Optional pre-check.** On a server that might already have Ola or other maintenance jobs, run
`Database/Scripts/Maintenance/CheckMaintenanceSolution.sql` first (read-only). It shows where Ola
lives and its version, and any Agent job that already calls `IndexOptimize` /
`DatabaseIntegrityCheck`. If such a job covers this database (`USER_DATABASES`, `ALL_DATABASES` or
its name), it would maintain the database twice: exclude it there
(`@Databases = 'USER_DATABASES, -CollateralAppraisal'`) or do not run our script.

**If Ola is already in `master`:** the script leaves it alone when it is the same version (so
re-running is safe). If it is any **other** version, the script **stops with an error before
changing anything**. It never upgrades or downgrades an install that other jobs may depend on.

**Then:** set the three variables at the top of the first batch, run the whole file, and verify
with section 6.

## 2. Jobs

The variables are `@DatabaseName` (default `CollateralAppraisal`), `@OlaDatabase` (default
`master`) and `@JobOwner` (default `sa`). The script checks them first and stops, creating
nothing, if the database, owner login or `@OlaDatabase` is wrong. The jobs are created in one
transaction, and re-running drops and recreates only the four jobs below (matched by exact name).

| Job (suffix ` - <database>`) | Calls |
|---|---|
| `CAS - Statistics (nightly)` | `IndexOptimize`, statistics only |
| `CAS - Index optimize (weekly)` | `IndexOptimize`, indexes + statistics, `@TimeLimit = 7200` |
| `CAS - Integrity check (weekly)` | `DatabaseIntegrityCheck`, `CHECKDB` |
| `CAS - Cleanup (weekly)` | deletes `dbo.CommandLog` rows and these four jobs' history older than 30 days |

The script is the source of truth for the job steps. For reference, they are:

```sql
-- Nightly: statistics only (cheap, and the part that matters most — see §4)
EXECUTE dbo.IndexOptimize
    @Databases = 'CollateralAppraisal',
    @FragmentationLow = NULL, @FragmentationMedium = NULL, @FragmentationHigh = NULL,
    @UpdateStatistics = 'ALL', @OnlyModifiedStatistics = 'Y',
    @LogToTable = 'Y';

-- Weekly: indexes + statistics
EXECUTE dbo.IndexOptimize
    @Databases = 'CollateralAppraisal',
    @FragmentationLow = NULL,
    @FragmentationMedium = 'INDEX_REORGANIZE,INDEX_REBUILD_ONLINE,INDEX_REBUILD_OFFLINE',
    @FragmentationHigh   = 'INDEX_REBUILD_ONLINE,INDEX_REBUILD_OFFLINE',
    @FragmentationLevel1 = 5, @FragmentationLevel2 = 30,
    @MinNumberOfPages = 1000,
    @UpdateStatistics = 'ALL', @OnlyModifiedStatistics = 'Y',
    @TimeLimit = 7200,               -- stop starting new work after 2 h
    @LogToTable = 'Y';

-- Weekly: integrity
EXECUTE dbo.DatabaseIntegrityCheck
    @Databases = 'CollateralAppraisal',
    @CheckCommands = 'CHECKDB',
    @LogToTable = 'Y';
```

Backups follow the bank's own policy. This setup creates no backup job and never calls Ola's
`DatabaseBackup`.

## 3. Schedule — keep out of the application's window

The application runs its own jobs between **01:00 and about 05:00** every night: log and outbox
purges, block reappraisal and report cleanup. On the **2nd of each month** it also runs the
AS400 file interfaces in the same window. Heavy maintenance there competes for locks and I/O.

| Job | When |
|---|---|
| `CAS - Statistics (nightly)` | Daily 22:00 |
| `CAS - Index optimize (weekly)` | Sunday 22:30 (`@TimeLimit` stops it well before 01:00) |
| `CAS - Integrity check (weekly)` | Saturday 22:00 |
| `CAS - Cleanup (weekly)` | Sunday 21:30 |

These application times are only defaults. An admin can move them in the app
(**Scheduled Jobs** menu, `/admin/job-schedules`). Check there before fixing the window.

## 4. Notes specific to this database

- **Primary keys are GUID v7 (time-ordered).** Inserts append at the end of the index, so
  fragmentation grows slowly and the weekly index job is mostly a safety net. Stale statistics
  hurt much more, which is why they are refreshed every night.
- **`dbo.Logs` has high churn**: about 150k rows are inserted per day and a daily purge deletes
  them again. It will usually be the most fragmented table, which is expected.
- **The `hangfire` schema** also churns constantly, and `IndexOptimize` covers it automatically.
- **Spatial indexes** exist on the geo-point columns (Appraisal, Collateral, Request). They are
  slow to rebuild, and `@TimeLimit` protects the window.
- `INDEX_REBUILD_ONLINE` works only on Enterprise edition. On Standard edition Ola falls back to
  an offline rebuild, which locks the table. That is acceptable in the window above.

## 5. Failures and corruption

No e-mail is configured. A failed job shows in SQL Server Agent (job history, red icon) and in the
queries in section 6. Check them after the first weekend and then regularly. An integrity check
that fails silently is worse than none.

If `CHECKDB` reports corruption, **do not** run `REPAIR_ALLOW_DATA_LOSS`. Restore from the last
good backup and involve the vendor.

## 6. Verify it is running

Right after the script: the last result set lists the four jobs with their schedules and next run.
Then start the cheap one and look at the result:

```sql
EXEC msdb.dbo.sp_start_job @job_name = N'CAS - Statistics (nightly) - CollateralAppraisal';

-- Outcome of the jobs (run_status 1 = succeeded, 0 = failed; step_id 0 = the whole job)
SELECT TOP (20) j.name, h.step_id, h.run_status, h.run_date, h.run_time, LEFT(h.message, 200) AS message
FROM msdb.dbo.sysjobhistory h JOIN msdb.dbo.sysjobs j ON j.job_id = h.job_id
WHERE j.name LIKE N'CAS - %'
ORDER BY h.instance_id DESC;

-- Last clean CHECKDB (should be < 8 days old once the first Saturday has passed)
SELECT DATABASEPROPERTYEX('CollateralAppraisal', 'LastGoodCheckDbTime');

-- What the jobs actually did recently (non-zero ErrorNumber = investigate)
SELECT TOP (50) StartTime, EndTime, CommandType, ObjectName, IndexName, ErrorNumber
FROM master.dbo.CommandLog   -- or the database you used as @OlaDatabase
ORDER BY ID DESC;
```
