/*
=============================================================================
 PdpaRedactCustomerData.sql
-----------------------------------------------------------------------------
 Redacts (anonymizes) ALL customer personal data in the CURRENT database:
 names, contact numbers, house numbers / addresses, contact persons, owner
 names and the e-mail addresses captured on quotation / meeting records.
 Every targeted value is overwritten with a fixed placeholder so no real PII
 survives anywhere in the system.

 PURPOSE (PDPA)
   Scrub a RESTORED COPY of production before that data is used in a lower
   environment (UAT / training / test). Thailand's PDPA does not allow real,
   identifiable customer data to live outside production without a lawful
   basis, so the copy must be anonymized before it is handed over.

 *** NON-PRODUCTION COPY ONLY  ***
   This is IRREVERSIBLE. It blanks the owner / contact / customer names that
   the running application reads, so it MUST NOT be run against the live
   production database. The @ConfirmNonProd gate below is a deliberate
   speed-bump, not a substitute for checking which server you are on.

 DIAGNOSTIC / MAINTENANCE ONLY - this script is NOT run by DbUp (it lives
 under Scripts\Maintenance\, not Scripts\Views|StoredProcedures|Functions\).
 Run it by hand against the restored CollateralAppraisal copy.

 HOW TO USE
   1. Confirm: set @ConfirmNonProd = 1 (asserts this is NOT production).
   2. Dry run: leave @DryRun = 1. Every UPDATE runs inside a transaction that
      is ROLLED BACK at the end; per-table affected-row counts are PRINTed so
      you can review the blast radius. Nothing is changed.
   3. Apply: set @DryRun = 0 to COMMIT the redaction on the copy.

 CAUTIONS
   * Run @ConfirmNonProd = 0 first to prove the safety gate stops the script.
   * Customer PII is denormalized: the source request/collateral rows AND the
     immutable appraisal snapshots both carry owner/contact names. This script
     scrubs every known location - re-verify after any schema change that adds
     a new PII column (the COL_LENGTH guards make unknown columns no-ops, so a
     newly added column would be silently skipped).
   * Raw SQL bypasses EF's audit interceptor, so this script stamps
     UpdatedBy / UpdatedAt itself on every touched table that has those columns.
=============================================================================
*/

SET NOCOUNT ON;

-------------------------------------------------------------------------------
-- Config
-------------------------------------------------------------------------------
DECLARE @ConfirmNonProd BIT            = 0;                       -- MUST be 1 to run. Asserts this is a non-prod copy.
DECLARE @DryRun         BIT            = 1;                       -- 1 = count only (rolls back), 0 = apply (commits)
DECLARE @Redact         NVARCHAR(50)   = N'[REDACTED]';           -- placeholder for names / phones / addresses
DECLARE @RedactEmail    NVARCHAR(50)   = N'redacted@example.com'; -- placeholder for e-mail columns (keeps a valid shape)
DECLARE @Actor          NVARCHAR(10)   = N'PDPAREDACT';           -- written to UpdatedBy for the audit trail (UpdatedBy/CreatedBy are nvarchar(10) - keep <= 10 chars)

-------------------------------------------------------------------------------
-- Safety gate
-------------------------------------------------------------------------------
IF @ConfirmNonProd <> 1
BEGIN
    RAISERROR('REFUSED: set @ConfirmNonProd = 1 to confirm this is a NON-PRODUCTION copy. This script permanently anonymizes all customer PII and must never run against production.', 16, 1);
    RETURN;
END

PRINT '--- PDPA customer-data redaction ---';
PRINT '--- Mode: ' + CASE WHEN @DryRun = 1 THEN 'DRY RUN (no changes will be committed)' ELSE 'APPLY (changes WILL be committed)' END;
PRINT '';

BEGIN TRY
    BEGIN TRAN;

    ---------------------------------------------------------------------------
    -- request schema
    ---------------------------------------------------------------------------
    IF COL_LENGTH('request.RequestCustomers', 'Name') IS NOT NULL
    BEGIN
        UPDATE request.RequestCustomers
        SET Name = @Redact, ContactNumber = @Redact;
        PRINT '  request.RequestCustomers: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('request.RequestDetails', 'HouseNumber') IS NOT NULL
    BEGIN
        -- House-level locators + contact + loan id. Admin-area geography
        -- (SubDistrict/District/Province/Postcode) is left intact - not identifying.
        UPDATE request.RequestDetails
        SET HouseNumber         = @Redact,
            ProjectName         = @Redact,
            Moo                 = @Redact,
            Soi                 = @Redact,
            Road                = @Redact,
            ContactPersonName   = @Redact,
            ContactPersonPhone  = @Redact,
            LoanApplicationNumber = @Redact,
            AppointmentLocation = @Redact;
        PRINT '  request.RequestDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('request.RequestTitles', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE request.RequestTitles
        SET OwnerName       = @Redact,
            HouseNumber     = @Redact,
            ProjectName     = @Redact,
            Moo             = @Redact,
            Soi             = @Redact,
            Road            = @Redact,
            DopaHouseNumber = @Redact,
            DopaProjectName = @Redact,
            DopaMoo         = @Redact,
            DopaSoi         = @Redact,
            DopaRoad        = @Redact;
        PRINT '  request.RequestTitles: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    ---------------------------------------------------------------------------
    -- collateral schema
    ---------------------------------------------------------------------------
    IF COL_LENGTH('collateral.CollateralMasters', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE collateral.CollateralMasters
        SET OwnerName = @Redact, CustomerName = @Redact;
        PRINT '  collateral.CollateralMasters: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('collateral.LandDetails', 'Street') IS NOT NULL
    BEGIN
        UPDATE collateral.LandDetails
        SET Street = @Redact, Village = @Redact;
        PRINT '  collateral.LandDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('collateral.ProjectUnits', 'HouseNumber') IS NOT NULL
    BEGIN
        UPDATE collateral.ProjectUnits
        SET HouseNumber = @Redact;
        PRINT '  collateral.ProjectUnits: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    ---------------------------------------------------------------------------
    -- appraisal schema  (per-appraisal snapshots + billing / e-mail records)
    ---------------------------------------------------------------------------
    IF COL_LENGTH('appraisal.LandAppraisalDetails', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.LandAppraisalDetails SET OwnerName = @Redact, Street = @Redact;
        PRINT '  appraisal.LandAppraisalDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.BuildingAppraisalDetails', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.BuildingAppraisalDetails SET OwnerName = @Redact, HouseNumber = @Redact;
        PRINT '  appraisal.BuildingAppraisalDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.CondoAppraisalDetails', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.CondoAppraisalDetails SET OwnerName = @Redact, Street = @Redact;
        PRINT '  appraisal.CondoAppraisalDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.VehicleAppraisalDetails', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.VehicleAppraisalDetails SET OwnerName = @Redact;
        PRINT '  appraisal.VehicleAppraisalDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.VesselAppraisalDetails', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.VesselAppraisalDetails SET OwnerName = @Redact;
        PRINT '  appraisal.VesselAppraisalDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.MachineryAppraisalDetails', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.MachineryAppraisalDetails SET OwnerName = @Redact;
        PRINT '  appraisal.MachineryAppraisalDetails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.MachineryAppraisalSummaries', 'Owner') IS NOT NULL
    BEGIN
        UPDATE appraisal.MachineryAppraisalSummaries SET [Owner] = @Redact;
        PRINT '  appraisal.MachineryAppraisalSummaries: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.Appointments', 'ContactPerson') IS NOT NULL
    BEGIN
        UPDATE appraisal.Appointments SET ContactPerson = @Redact, ContactPhone = @Redact;
        PRINT '  appraisal.Appointments: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.Invoices', 'CustomerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.Invoices SET CustomerName = @Redact;
        PRINT '  appraisal.Invoices: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.InvoiceItems', 'CustomerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.InvoiceItems SET CustomerName = @Redact;
        PRINT '  appraisal.InvoiceItems: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.QuotationEmails', 'From') IS NOT NULL
    BEGIN
        UPDATE appraisal.QuotationEmails
        SET [From] = @RedactEmail, [To] = @RedactEmail, [Cc] = @RedactEmail, [Bcc] = @RedactEmail;
        PRINT '  appraisal.QuotationEmails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.CompanyQuotations', 'SubmittedByEmail') IS NOT NULL
    BEGIN
        UPDATE appraisal.CompanyQuotations SET SubmittedByEmail = @RedactEmail;
        PRINT '  appraisal.CompanyQuotations: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.ProjectLands', 'OwnerName') IS NOT NULL
    BEGIN
        UPDATE appraisal.ProjectLands SET OwnerName = @Redact, Street = @Redact;
        PRINT '  appraisal.ProjectLands: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.Projects', 'HouseNumber') IS NOT NULL
    BEGIN
        UPDATE appraisal.Projects SET HouseNumber = @Redact;
        PRINT '  appraisal.Projects: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    IF COL_LENGTH('appraisal.ProjectUnits', 'HouseNumber') IS NOT NULL
    BEGIN
        UPDATE appraisal.ProjectUnits SET HouseNumber = @Redact;
        PRINT '  appraisal.ProjectUnits: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    ---------------------------------------------------------------------------
    -- workflow schema
    ---------------------------------------------------------------------------
    IF COL_LENGTH('workflow.MeetingInvitationEmails', 'To') IS NOT NULL
    BEGIN
        UPDATE workflow.MeetingInvitationEmails SET [To] = @RedactEmail;
        PRINT '  workflow.MeetingInvitationEmails: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
    END

    ---------------------------------------------------------------------------
    -- Audit stamp: bump UpdatedBy / UpdatedAt on every touched table that has
    -- those columns (raw SQL bypasses EF's AuditableEntityInterceptor). Owned
    -- value-object tables (RequestCustomers / RequestDetails) carry no audit
    -- columns, so their parent request.Requests is stamped instead. Uses local
    -- time (GETDATE), never UTC, per project convention.
    ---------------------------------------------------------------------------
    PRINT '';
    PRINT '--- Stamping audit columns ---';

    DECLARE @Touched TABLE (FullName SYSNAME);
    INSERT INTO @Touched (FullName) VALUES
        ('request.Requests'),                       -- parent of the owned RequestCustomers / RequestDetails
        ('request.RequestTitles'),
        ('collateral.CollateralMasters'),
        ('collateral.LandDetails'),
        ('collateral.ProjectUnits'),
        ('appraisal.LandAppraisalDetails'),
        ('appraisal.BuildingAppraisalDetails'),
        ('appraisal.CondoAppraisalDetails'),
        ('appraisal.VehicleAppraisalDetails'),
        ('appraisal.VesselAppraisalDetails'),
        ('appraisal.MachineryAppraisalDetails'),
        ('appraisal.MachineryAppraisalSummaries'),
        ('appraisal.Appointments'),
        ('appraisal.Invoices'),
        ('appraisal.InvoiceItems'),
        ('appraisal.QuotationEmails'),
        ('appraisal.CompanyQuotations'),
        ('appraisal.ProjectLands'),
        ('appraisal.Projects'),
        ('appraisal.ProjectUnits'),
        ('workflow.MeetingInvitationEmails');

    DECLARE @Tbl SYSNAME, @Stmt NVARCHAR(MAX);
    DECLARE audit_cur CURSOR LOCAL FAST_FORWARD FOR SELECT FullName FROM @Touched;
    OPEN audit_cur;
    FETCH NEXT FROM audit_cur INTO @Tbl;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF COL_LENGTH(@Tbl, 'UpdatedBy') IS NOT NULL AND COL_LENGTH(@Tbl, 'UpdatedAt') IS NOT NULL
        BEGIN
            SET @Stmt = N'UPDATE ' + @Tbl + N' SET UpdatedBy = @Actor, UpdatedAt = GETDATE();';
            EXEC sp_executesql @Stmt, N'@Actor NVARCHAR(10)', @Actor = @Actor;
            PRINT '  stamped ' + @Tbl + ': ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows';
        END
        FETCH NEXT FROM audit_cur INTO @Tbl;
    END
    CLOSE audit_cur;
    DEALLOCATE audit_cur;

    ---------------------------------------------------------------------------
    -- Commit or roll back
    ---------------------------------------------------------------------------
    PRINT '';
    IF @DryRun = 1
    BEGIN
        ROLLBACK TRAN;
        PRINT '--- DRY RUN complete: transaction ROLLED BACK, no changes saved. Set @DryRun = 0 to apply. ---';
    END
    ELSE
    BEGIN
        COMMIT TRAN;
        PRINT '--- APPLIED: customer PII redacted and committed. ---';
    END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '--- ERROR: transaction rolled back, no changes saved. ---';
    THROW;
END CATCH
