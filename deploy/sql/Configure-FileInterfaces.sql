/* =====================================================================================
   integration.FileInterfaceConfigs — per-environment paths and file names
   =====================================================================================

   RUN THIS BY HAND, ONCE PER ENVIRONMENT, AFTER A MIGRATE. It is deliberately NOT a DbUp
   script in Database/Migration/Scripts.

   Why not DbUp: every value below is an environment fact. `D:\SFTP\FTP_DATA\CAS` and
   `\\172.20.0.14\Data_AS400\Risk\CAS` do not exist on a developer's laptop or in CI, and a
   journalled script would push them there too. Per CLAUDE.md, what ships in a migration is
   what CODE READS BY NAME — that is the InterfaceCode column, and all five rows are already
   seeded (20260610040218_AddFileInterfaceConfigs, 20260808120000_SeedData_HostCollateral-
   LinkInterface, 20260830120000_SeedData_RegulatoryExcelInterface). Only the naming and
   destination columns are left, and those are operator settings.

   There are no INSERTs here for the same reason: if an UPDATE reports 0 rows affected, the
   database has not been migrated far enough — fix that rather than inserting a row by hand,
   or the InterfaceCode may not match what C# looks up.

   Idempotent: every statement sets an absolute value. Safe to re-run.

   -------------------------------------------------------------------------------------
   ⚠ BEFORE YOU RUN: @CasDir AND @RdtDir MUST BE SFTP PATHS, NOT WINDOWS PATHS
   -------------------------------------------------------------------------------------
   In UAT/production appsettings sets FileTransfer:Inbound:FileSource and
   FileTransfer:Outbound:FileSource to "Sftp", so these two directories are handed to
   SftpFileSink / SftpInboundFileSource as REMOTE paths — what the SFTP account sees after
   login, not the D:\ path on the file server's own disk.

   If the SFTP account's home is  D:\SFTP\FTP_DATA   →  '/CAS'  and  '/RDT'
   If it is                       D:\SFTP            →  '/FTP_DATA/CAS'  and  '/FTP_DATA/RDT'

   Confirm it, do not guess:   sftp <user>@<host>   then   pwd   and   ls -l

   Guessing wrong fails SILENTLY in both directions. Outbound, SftpFileSink.EnsureRemote-
   Directory creates the path recursively, so the export "succeeds" into a folder nobody
   collects. Inbound, a directory with no matching file is simply a no-op run.

   @XlsxDir is different and stays a UNC path: the regulatory workbook is written through the
   keyed FileSystem sink (OutboundFileSinkKeys.FileSystem → LocalFileSink) precisely so a
   \\server\share destination is not handed to SFTP. The CAS-Api app pool identity needs
   Modify rights on that share — see deploy/README.md.
   ===================================================================================== */

DECLARE @CasDir  nvarchar(500) = N'/CAS';                                -- ⟵ CONFIRM (COLLATREV in, COLLAT in, CAS_APPRE out)
DECLARE @RdtDir  nvarchar(500) = N'/RDT';                                -- ⟵ CONFIRM (RDTCLSINT4 out)
DECLARE @XlsxDir nvarchar(500) = N'\\172.20.0.14\Data_AS400\Risk\CAS';   -- SMB share, written by the app pool account

/* -------------------------------------------------------------------------------------
   Inbound — AS400 COLLATREV, the reappraisal due-list.  AS400_COLLATREV_20260831.txt
   -------------------------------------------------------------------------------------
   ProcessedDirectory = NULL turns archiving OFF. The drop folder belongs to AS400 and we
   cannot move anything out of it. integration.InboundFileLogs (the ledger) is what stops a
   file being ingested twice, so archiving is only a local-run convenience — this is the
   change 20260831090000_SeedData_As400JobScheduleAndTimeZone.sql left documented as a no-op
   because whether a host can move files is an environment fact.

   Side effect worth knowing: quarantine also moves files, into {ProcessedDirectory}/failed.
   With NULL there is nowhere to move them, so a bad file stays in the inbox and the only
   record of it is its InboundFileLogs row (Status = Quarantined) and the Seq entry. That is
   also why it will not be retried forever — the ledger, not the folder, is the memory. */
UPDATE integration.FileInterfaceConfigs
SET [Directory]        = @CasDir,
    ProcessedDirectory = NULL,
    FilePattern        = N'AS400_COLLATREV_*.txt',
    IsActive           = 1
WHERE InterfaceCode = 'REAPPRAISAL';

/* -------------------------------------------------------------------------------------
   Inbound — AS400 COLLATLINK, the appraisal-number → CCDCID map.  AS400_COLLAT_20260826.txt
   -------------------------------------------------------------------------------------
   The pattern changes: the seeded value was 'AS400_COLLATLINK_*.txt', production sends
   'AS400_COLLAT_*.txt'.

   The two inbound feeds now share one folder, and the patterns still do not collide:
   'AS400_COLLATREV_20260831.txt' does NOT start with 'AS400_COLLAT_' — character 13 is 'R',
   not '_'. True for both matchers, the Win32 glob in LocalInboundFileSource and the
   hand-rolled StartsWith/EndsWith in SftpInboundFileSource.

   The trailing _yyyyMMdd is not cosmetic. HostCollateralLinkFileParser.ParseFilenameDate
   reads the date out of the NAME (last '_' token, exactly 8 digits) and it becomes
   LastSeenFileDate, which is how a full-replace feed decides what the bank no longer holds
   and how an out-of-order older file is refused. A file arriving without it is quarantined,
   never applied. */
UPDATE integration.FileInterfaceConfigs
SET [Directory]        = @CasDir,
    ProcessedDirectory = NULL,
    FilePattern        = N'AS400_COLLAT_*.txt',
    IsActive           = 1
WHERE InterfaceCode = 'HOST_COLLATERAL_LINK';

/* -------------------------------------------------------------------------------------
   Outbound — completed appraisal prices back to the host.  CAS_APPRE_20260630.txt
   -------------------------------------------------------------------------------------
   The date format drops from yyyyMMddHHmmss to yyyyMMdd, so two runs on the same day write
   the same name and the second overwrites the first (SFTP upload is canOverride: true).
   The scheduled run at 07:00 cannot hit this: the ledger marks rows sent, so a second run
   finds no unsent rows and returns before writing anything. It only matters if someone
   triggers the job by hand later the same day after more appraisals have closed — that file
   would replace the morning's, which AS400 may not have collected yet. */
UPDATE integration.FileInterfaceConfigs
SET FileNamePrefix     = N'CAS_APPRE_',
    FileNameDateFormat = N'yyyyMMdd',
    FileExtension      = N'txt',
    [Directory]        = @CasDir,
    ProcessedDirectory = NULL,
    FilePattern        = NULL,
    IsActive           = 1
WHERE InterfaceCode = 'COLLATERAL_RESULT';

/* -------------------------------------------------------------------------------------
   Outbound — monthly regulatory (Basel/RDT) fixed-width snapshot.  RDTCLSINT4.txt
   -------------------------------------------------------------------------------------
   ONE FIXED NAME, NO DATE, OVERWRITTEN EVERY MONTH — that is what RDT collects, and it means
   there is no history of past months on the share. Intentional; the data is reproducible
   from collateral.vw_RegulatoryExport.

   FileNameDateFormat = '' is the instruction "do not put a date in the name". An empty
   string, not NULL: NULL means "use the job's own default", which is yyyyMMdd. This only
   works from the commit that added OutboundFileName.Build — before it, an empty format
   reached DateTime.ToString(""), which .NET reads as the standard "G" specifier and returns
   '6/30/2026 2:00:00 AM'. Slashes and colons cannot go in a file name, so on an older build
   this row makes the job throw instead of writing an undated file. Deploy first, then run. */
UPDATE integration.FileInterfaceConfigs
SET FileNamePrefix     = N'RDTCLSINT4',
    FileNameDateFormat = N'',
    FileExtension      = N'txt',
    [Directory]        = @RdtDir,
    ProcessedDirectory = NULL,
    FilePattern        = NULL,
    IsActive           = 1
WHERE InterfaceCode = 'REGULATORY';

/* -------------------------------------------------------------------------------------
   Outbound — the readable companion the Risk team opens by hand.
   CAS RE Listing_20260818.xlsx   (yes, the name contains spaces)
   -------------------------------------------------------------------------------------
   FileNameDateFormat MUST be spelled out here even though yyyyMMdd looks like the default.
   RegulatoryExportJob.WriteExcelAsync falls back per-field to the REGULATORY row, and that
   row now carries the empty format above — leaving this one NULL would quietly strip the
   date from the workbook as well, and the Risk team keeps every month's file.

   Set IsActive = 0 to stop producing the workbook entirely; the .txt for RDT is governed by
   the separate REGULATORY row and goes out either way. */
UPDATE integration.FileInterfaceConfigs
SET FileNamePrefix     = N'CAS RE Listing_',
    FileNameDateFormat = N'yyyyMMdd',
    FileExtension      = N'xlsx',
    [Directory]        = @XlsxDir,
    ProcessedDirectory = NULL,
    FilePattern        = NULL,
    IsActive           = 1
WHERE InterfaceCode = 'REGULATORY_XLSX';

/* -------------------------------------------------------------------------------------
   Verify. Every row must be present and read the way the table below says.

     REAPPRAISAL           In   -                 -          -      @CasDir   AS400_COLLATREV_*.txt
     HOST_COLLATERAL_LINK  In   -                 -          -      @CasDir   AS400_COLLAT_*.txt
     COLLATERAL_RESULT     Out  CAS_APPRE_        yyyyMMdd   txt    @CasDir   -
     REGULATORY            Out  RDTCLSINT4        (empty)    txt    @RdtDir   -
     REGULATORY_XLSX       Out  CAS RE Listing_   yyyyMMdd   xlsx   @XlsxDir  -

   FileInterfaceConfigProvider caches each row for 60 seconds, so wait a minute before
   triggering a job from the Hangfire dashboard or it will use the values from before. */
SELECT InterfaceCode,
       Direction,
       FileNamePrefix,
       FileNameDateFormat,
       FileExtension,
       [Directory],
       ProcessedDirectory,
       FilePattern,
       IsActive
FROM integration.FileInterfaceConfigs
ORDER BY Direction DESC, InterfaceCode;
