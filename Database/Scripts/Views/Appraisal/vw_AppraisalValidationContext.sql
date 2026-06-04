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
    END                                                                     AS HasNoAppraisedValue

FROM appraisal.Appraisals a
         LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
WHERE a.IsDeleted = 0
