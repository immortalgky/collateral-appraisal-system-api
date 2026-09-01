/*
  20260901090000_Backfill_MachineryRegistrationFromOtherBlob.sql

  Purpose : Populate the new appraisal.MachineryAppraisalDetails columns
            (RegistrationStatus, InstallationStatus, InvoiceNumber, IsPriceCertified) for machines
            created before those columns existed, then retire the blob they were stored in.

  Why     : AppraisalCreationService.CreateMachineryProperty used to concatenate the request
            title's machine status into the free-text `Other` column, e.g.
                "RegistrationStatus=True; Invoice=INV001; InstallationStatus=1"
            Nothing downstream could query or group by it. The fields are real columns now, but
            existing rows still carry the values only inside that string. AppraisalProperties has
            no FK back to request.RequestTitles, so the blob is the only source available.

  Rule    : Touch ONLY rows whose `Other` still matches the exact machine-generated shape
            (starts with 'RegistrationStatus='). Anything an appraiser typed by hand does not
            match and is left alone. After parsing, `Other` is cleared on those rows so the data
            does not live in two places.

  Safety  : Idempotent — after the first run no row matches the pattern any more. Rows that never
            had the blob keep the column defaults (RegistrationStatus=0, IsPriceCertified=1) and
            are then corrected by the final certification pass, which is safe to re-run.
*/

SET NOCOUNT ON;

-- ── 1. Parse the generated blob ──────────────────────────────────────────────────────────────
-- Segments are joined with "; " and always appear in this order when present:
--   RegistrationStatus=<True|False>[; Invoice=<n>][; InstallationStatus=<code>]
-- A trailing ';' is appended so the LAST segment terminates the same way the others do.
WITH Src AS (
    SELECT
        mad.Id,
        Blob     = CAST(mad.Other AS nvarchar(max)) + N';',
        RegPos   = LEN(N'RegistrationStatus=') + 1,
        InvPos   = NULLIF(CHARINDEX(N'Invoice=', mad.Other), 0),
        InstPos  = NULLIF(CHARINDEX(N'InstallationStatus=', mad.Other), 0)
    FROM appraisal.MachineryAppraisalDetails mad
    WHERE mad.Other LIKE N'RegistrationStatus=%'
),
Parsed AS (
    SELECT
        Id,
        RegistrationText =
            SUBSTRING(Blob, RegPos, CHARINDEX(N';', Blob, RegPos) - RegPos),
        InvoiceText =
            SUBSTRING(Blob, InvPos + LEN(N'Invoice='),
                      CHARINDEX(N';', Blob, InvPos) - InvPos - LEN(N'Invoice=')),
        InstallationText =
            SUBSTRING(Blob, InstPos + LEN(N'InstallationStatus='),
                      CHARINDEX(N';', Blob, InstPos) - InstPos - LEN(N'InstallationStatus='))
    FROM Src
)
UPDATE mad
SET mad.RegistrationStatus = CASE WHEN LTRIM(RTRIM(p.RegistrationText)) = N'True' THEN 1 ELSE 0 END,
    mad.InvoiceNumber      = NULLIF(LEFT(LTRIM(RTRIM(p.InvoiceText)), 20), N''),
    mad.InstallationStatus = NULLIF(LEFT(LTRIM(RTRIM(p.InstallationText)), 10), N''),
    -- Retire the blob: every segment it held now has its own column.
    mad.Other              = NULL
FROM appraisal.MachineryAppraisalDetails mad
JOIN Parsed p ON p.Id = mad.Id;

PRINT CONCAT('Backfilled machinery registration columns from Other blob: ', @@ROWCOUNT, ' row(s).');

-- ── 2. Apply the certification invariant ─────────────────────────────────────────────────────
-- Mirrors MachineryAppraisalDetail.NormalizePriceCertification(): a price can only be certified
-- for a machine that is registered and not still under procurement (MachineStatus code '2').
UPDATE appraisal.MachineryAppraisalDetails
SET IsPriceCertified = 0
WHERE IsPriceCertified = 1
  AND (RegistrationStatus = 0 OR InstallationStatus = N'2');

PRINT CONCAT('Forced IsPriceCertified=0 on ineligible machines: ', @@ROWCOUNT, ' row(s).');
