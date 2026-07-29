CREATE
OR ALTER
VIEW appraisal.vw_AppraisalValidationContext AS
SELECT
    -- ── Identity ──────────────────────────────────────────────────────────
    a.Id                                                                    AS AppraisalId,

    -- ── Scalar appraisal fields ───────────────────────────────────────────
    a.Status,
    a.AppraisalType,
    a.Purpose,
    a.Channel,
    a.BankingSegment,
    CAST(a.IsPma AS tinyint)                                               AS IsPma,
    a.FacilityLimit,

    -- ── Appraised value (from ValuationAnalyses; NULL when not yet set) ───
    va.AppraisedValue,

    -- ── Property-level aggregate facts ───────────────────────────────────
    (
        SELECT COUNT(*)
        FROM appraisal.AppraisalProperties ap
        WHERE ap.AppraisalId = a.Id
    )                                                                       AS PropertyCount,

    -- Number of properties whose LandAppraisalDetail is missing a LandOffice.
    -- A property is counted only if a LandAppraisalDetail row exists for it
    -- (land-type properties). Non-land types are excluded from this count.
    (
        SELECT COUNT(*)
        FROM appraisal.AppraisalProperties ap
                 LEFT JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap.Id
        WHERE ap.AppraisalId = a.Id
          AND lad.Id IS NOT NULL           -- has a land detail row
          AND (lad.LandOffice IS NULL OR lad.LandOffice = '')
    )                                                                       AS PropertiesMissingLandOfficeCount,

    -- Number of land properties that have no LandTitle row with a non-empty TitleNumber.
    (
        SELECT COUNT(*)
        FROM appraisal.AppraisalProperties ap
                 LEFT JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap.Id
        WHERE ap.AppraisalId = a.Id
          AND lad.Id IS NOT NULL           -- has a land detail row
          AND NOT EXISTS (
              SELECT 1
              FROM appraisal.LandTitles lt
              WHERE lt.LandAppraisalDetailId = lad.Id
                AND lt.TitleNumber IS NOT NULL
                AND lt.TitleNumber <> ''
          )
    )                                                                       AS PropertiesMissingTitleCount,

    -- 1 when no ValuationAnalysis row exists OR AppraisedValue is NULL / zero.
    CASE
        WHEN va.Id IS NULL OR ISNULL(va.AppraisedValue, 0) = 0 THEN 1
        ELSE 0
    END                                                                     AS HasNoAppraisedValue,

    -- ── Assignment-stage readiness (ext-appraisal-assignment) ────────────
    -- Active (non-cancelled) appointment scheduled for any of this appraisal's assignments.
    -- Includes 'Pending' (awaiting reschedule approval) so a mid-reschedule appointment still counts.
    CASE WHEN EXISTS (
        SELECT 1
        FROM appraisal.Appointments apt
                 INNER JOIN appraisal.AppraisalAssignments aa ON apt.AssignmentId = aa.Id
        WHERE aa.AppraisalId = a.Id
          AND apt.Status IN ('Appointed', 'Pending')
    ) THEN 1 ELSE 0 END                                                     AS HasAppointment,

    -- Appointment scheduled in the future. Local time per the never-UTC rule (GETDATE()).
    CASE WHEN EXISTS (
        SELECT 1
        FROM appraisal.Appointments apt
                 INNER JOIN appraisal.AppraisalAssignments aa ON apt.AssignmentId = aa.Id
        WHERE aa.AppraisalId = a.Id
          AND apt.Status IN ('Appointed', 'Pending')
          AND apt.AppointmentDateTime > GETDATE()
    ) THEN 1 ELSE 0 END                                                     AS AppointmentInFuture,

    -- Highest fee-after-VAT across the appraisal's assignments (0 when no fee set).
    ISNULL((
        SELECT MAX(af.TotalFeeAfterVAT)
        FROM appraisal.AppraisalFees af
                 INNER JOIN appraisal.AppraisalAssignments aa ON af.AssignmentId = aa.Id
        WHERE aa.AppraisalId = a.Id
    ), 0)                                                                    AS TotalFeeAfterVat,

    -- An appraiser is assigned (external company appraiser, internal appraiser, or assignee).
    CASE WHEN EXISTS (
        SELECT 1
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AppraisalId = a.Id
          AND (NULLIF(aa.AssigneeCompanyId, '') IS NOT NULL
            OR NULLIF(aa.AssigneeUserId, '') IS NOT NULL)
    ) THEN 1 ELSE 0 END                                                     AS HasAssignedAppraiser,

    -- An EXTERNAL assignment must name the company that did the work. True when the active
    -- assignment is not External (nothing to name), or when it is and a company is recorded.
    -- Off-system engagements are the case this exists for: the bank engaged the company outside
    -- the system, so no company-selection ever ran and only the keyer can supply it. Without a
    -- company there is no fee to resolve, and the book and AS400 feed print a blank company.
    CASE WHEN EXISTS (
        SELECT 1
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AppraisalId = a.Id
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
          AND aa.AssignmentType = 'External'
          AND NULLIF(aa.AssigneeCompanyId, '') IS NULL
    ) THEN 0 ELSE 1 END                                                     AS ExternalCompanyRecorded,

    -- An EXTERNAL assignment must have a real appraisal date behind it. Two legitimate sources:
    -- a non-cancelled appointment (in-system work), or an off-system engagement where the keyer
    -- typed the date off the company's book (AssignmentMethod = 'Offline').
    --
    -- Checking ValuationDate for a value would prove nothing: AppraisalCreationService seeds the
    -- ValuationAnalyses row at creation, so the column is never empty — an in-system external case
    -- that never had an appointment carries the time of the last pricing recompute, which reads as
    -- an appraisal date but is not one. This catches that as well as an unrecorded book date.
    CASE WHEN EXISTS (
        SELECT 1
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AppraisalId = a.Id
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
          AND aa.AssignmentType = 'External'
          AND aa.AssignmentMethod <> 'Offline'
          AND NOT EXISTS (
              SELECT 1
              FROM appraisal.Appointments ap
              WHERE ap.AssignmentId = aa.Id
                AND ap.Status <> 'Cancelled'
          )
    ) THEN 0 ELSE 1 END                                                     AS ExternalAppraisalDateRecorded,

    -- ── Execution-stage readiness ────────────────────────────────────────
    -- Count of selected pricing methods (property-group-subject analyses).
    (
        SELECT COUNT(pam.Id)
        FROM appraisal.PricingAnalysisMethods pam
                 INNER JOIN appraisal.PricingAnalysisApproaches paa ON pam.ApproachId = paa.Id
                 INNER JOIN appraisal.PricingAnalysis pa ON paa.PricingAnalysisId = pa.Id
                 INNER JOIN appraisal.PropertyGroups pg ON pa.AnchorId = pg.Id
        WHERE pg.AppraisalId = a.Id
          AND pa.SubjectType = 0
          AND pam.IsSelected = 1
    )                                                                       AS SelectedPricingMethodCount,

    -- Count of (non-deleted) market comparables linked to the appraisal.
    (
        SELECT COUNT(ac.Id)
        FROM appraisal.AppraisalComparables ac
                 INNER JOIN appraisal.MarketComparables mc ON ac.MarketComparableId = mc.Id
        WHERE ac.AppraisalId = a.Id
          AND mc.IsDeleted = 0
    )                                                                       AS ComparableCount,

    -- Count of gallery photos attached to the appraisal.
    (
        SELECT COUNT(*)
        FROM appraisal.AppraisalGallery g
        WHERE g.AppraisalId = a.Id
    )                                                                       AS PhotoCount

FROM appraisal.Appraisals a
         LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
WHERE a.IsDeleted = 0
