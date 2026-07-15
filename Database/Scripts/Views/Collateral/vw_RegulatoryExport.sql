-- CAS-AS400-Regulatory export view.
-- One row per active IsMaster collateral master (IsDeleted=0, IsMaster=1).
-- Supplies all typed columns consumed by RegulatoryExportRow / RegulatoryFileWriter (300-char layout).
--
-- Engagement selection:
--   Earliest: MIN(AppraisalDate) per master (tie-break CreatedAt ASC)  → application-id (prev)
--   Latest:   MAX(AppraisalDate) per master (tie-break CreatedAt DESC) → newest application-id
--
-- Representative building: Sequence=1 CollateralEngagementBuilding on the latest engagement.
--   Used for BuildingTypeCode, BuildingArea, and parameter-table description lookup.
--
-- DOPA code: joined from parameter.DopaSubDistricts on the stored SubDistrict name (Land/LB/LS*
--   types only; Condo has no sub-district column on CondoDetails). If multiple DOPA rows share the
--   same NameTh, MIN(Code) is returned (deterministic best-effort; the spec says blank on ambiguity
--   but a stable single code is harmless and more useful).
--
-- Numeric range guards (mirror CollateralResult LifeYear pattern — one bad value must not abort run):
--   ConstructionProgressPercent: out of [0,100] → NULL (writer formats as 0.00)
--   LandAreaSqWa:                > 99999.99     → NULL (dec(7,2) max in the 8-char field 151-158)
--   BuildingArea/UsableArea:     > 99999.99     → NULL (dec(7,2) max in the 8-char field 159-166)
--   BuildingAge / NumberOfFloors: handled in C# writer (clamped to [0,999])
--
-- No JSON/snapshot column reads. GETDATE() only (application-locale time; never GETUTCDATE()).

CREATE OR ALTER VIEW collateral.vw_RegulatoryExport AS

WITH

-- Earliest engagement per master (AppraisalDate ASC, then CreatedAt ASC for tie-break)
EarliestEngagement AS (
    SELECT
        e.CollateralMasterId,
        e.AppraisalNumber  AS EarliestAppraisalNumber,
        e.AppraisalDate    AS EarliestAppraisalDate,
        e.AppraisalValue   AS EarliestAppraisalValue,
        ROW_NUMBER() OVER (
            PARTITION BY e.CollateralMasterId
            ORDER BY e.AppraisalDate ASC, e.CreatedAt ASC
        ) AS rn
    FROM collateral.CollateralEngagements e
),

-- Latest engagement per master (AppraisalDate DESC, then CreatedAt DESC for tie-break)
LatestEngagement AS (
    SELECT
        e.CollateralMasterId,
        e.Id                  AS LatestEngagementId,
        e.AppraisalNumber     AS LatestAppraisalNumber,
        e.AppraisalType       AS LatestAppraisalType,
        e.AppraisalDate       AS LatestAppraisalDate,
        e.AppraisalValue      AS LatestAppraisalValue,
        e.AppraisalCompanyId  AS LatestAppraisalCompanyId,
        ROW_NUMBER() OVER (
            PARTITION BY e.CollateralMasterId
            ORDER BY e.AppraisalDate DESC, e.CreatedAt DESC
        ) AS rn
    FROM collateral.CollateralEngagements e
),

-- Previous engagement per master: the 2nd-most-recent (rn=2 of the same DESC order as Latest).
-- Feeds the "Application Id" field (the previous appraisal number, not the earliest). NULL when the
-- master has only one engagement → writer emits blank.
PreviousEngagement AS (
    SELECT
        e.CollateralMasterId,
        e.AppraisalNumber  AS PreviousAppraisalNumber,
        ROW_NUMBER() OVER (
            PARTITION BY e.CollateralMasterId
            ORDER BY e.AppraisalDate DESC, e.CreatedAt DESC, e.Id DESC
        ) AS rn
    FROM collateral.CollateralEngagements e
),

-- Latest Progressive (construction-inspection) engagement per master
LatestProgressiveEngagement AS (
    SELECT
        e.CollateralMasterId,
        e.AppraisalDate  AS LatestProgressiveAppraisalDate,
        ROW_NUMBER() OVER (
            PARTITION BY e.CollateralMasterId
            ORDER BY e.AppraisalDate DESC, e.CreatedAt DESC
        ) AS rn
    FROM collateral.CollateralEngagements e
    WHERE e.AppraisalType = 'Progressive'
),

-- Representative building: Sequence=1 on the latest engagement
RepresentativeBuilding AS (
    SELECT
        ceb.EngagementId,
        ceb.BuildingTypeCode,
        ceb.BuildingAge,
        ceb.NumberOfFloors,
        CASE
            WHEN ceb.BuildingArea > 99999.99 THEN NULL
            ELSE ceb.BuildingArea
        END  AS BuildingArea,
        ROW_NUMBER() OVER (
            PARTITION BY ceb.EngagementId
            ORDER BY ceb.Sequence ASC
        ) AS rn
    FROM collateral.CollateralEngagementBuildings ceb
)

SELECT
    m.Id                                                        AS CollateralMasterId,
    m.CollateralType,
    m.HostCollateralId,

    -- Previous engagement (2nd-most-recent) → "Application Id" field
    prev.PreviousAppraisalNumber,

    -- Earliest engagement (feeds first valuation date + origination value)
    ee.EarliestAppraisalDate,
    ee.EarliestAppraisalValue,

    -- Latest engagement
    le.LatestAppraisalNumber,
    le.LatestAppraisalType,
    le.LatestAppraisalDate,
    le.LatestAppraisalValue,
    le.LatestAppraisalCompanyId,

    -- Latest Progressive engagement date
    pe.LatestProgressiveAppraisalDate,

    -- Under-construction flag and progress (Land/LB/LS* types only; driven by LandDetails)
    ISNULL(ld.IsUnderConstructionAtLastAppraisal, 0)  AS IsUnderConstruction,
    CASE
        WHEN ld.OverallConstructionProgressPercent IS NULL        THEN NULL
        WHEN ld.OverallConstructionProgressPercent < 0           THEN NULL
        WHEN ld.OverallConstructionProgressPercent > 100         THEN NULL
        ELSE ld.OverallConstructionProgressPercent
    END                                                           AS ConstructionProgressPercent,

    -- Land area (sq.wa): Land/LB/LS* types; guard > 99999.99 which would overflow the dec(7,2) field
    CASE
        WHEN ld.LandArea > 99999.99 THEN NULL
        ELSE ld.LandArea
    END                                                           AS LandAreaSqWa,

    -- Number of floors: building types only (LB/LSB/LS), from the representative engagement building.
    --   • Condo (U) / bare land / machinery: NULL → writer renders 0 (spec "else 0").
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN CAST(rb.NumberOfFloors AS int)
        ELSE NULL
    END                                                           AS NumberOfFloors,

    -- Building age (years): all building types + condo.
    --   • Building/L&B (LB, LSB, LS): representative building age from engagement buildings
    --   • Condo (U): BuildingAge from CondoDetails
    --   • Others (bare land, machinery): NULL
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN rb.BuildingAge
        WHEN m.CollateralType = 'U'                  THEN cd.BuildingAge
        ELSE NULL
    END                                                           AS BuildingAge,

    -- Building area (area utilization):
    --   • Building/L&B (LB, LSB, LS): representative building area from engagement buildings
    --   • Condo (U): UsableArea from CondoDetails
    --   • Others: NULL
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN rb.BuildingArea            -- already guarded ≤ 99999.99 in CTE
        WHEN m.CollateralType = 'U' AND cd.UsableArea <= 99999.99 THEN cd.UsableArea -- guard the condo path too (dec(7,2))
        ELSE NULL
    END                                                           AS BuildingArea,

    -- Building type code (Building/L&B/LS* only; blank for condo, bare land, machinery)
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN rb.BuildingTypeCode
        ELSE NULL
    END                                                           AS BuildingTypeCode,

    -- Building type description (EN) from parameter.Parameters (group='BuildingType')
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN bt.[description]
        ELSE NULL
    END                                                           AS BuildingTypeDescription,

    -- DOPA 6-digit sub-district code. Sourced from the official parameter.DopaSubDistricts table
    -- (the "DOPA Location" field), NOT TitleSubDistricts (the title-deed address list). Land/LB/LS*
    -- types use LandDetails.SubDistrict; condo (U) uses CondoDetails.SubDistrict (it has its own
    -- address columns). MIN(Code) is a deterministic best-effort when multiple DOPA rows share NameTh.
    CASE
        WHEN m.CollateralType IN ('L', 'LB', 'LSL', 'LSB', 'LS', 'U')
            THEN (
                SELECT MIN(dsd.Code)
                FROM parameter.DopaSubDistricts dsd
                WHERE dsd.NameTh = COALESCE(ld.SubDistrict, cd.SubDistrict)
            )
        ELSE NULL
    END                                                           AS DopaCode

FROM collateral.CollateralMasters m

-- Earliest engagement (rn=1)
LEFT JOIN EarliestEngagement ee
    ON  ee.CollateralMasterId = m.Id
    AND ee.rn                 = 1

-- Latest engagement (rn=1)
LEFT JOIN LatestEngagement le
    ON  le.CollateralMasterId = m.Id
    AND le.rn                 = 1

-- Previous engagement (rn=2 → the 2nd-most-recent)
LEFT JOIN PreviousEngagement prev
    ON  prev.CollateralMasterId = m.Id
    AND prev.rn                 = 2

-- Latest Progressive engagement (rn=1)
LEFT JOIN LatestProgressiveEngagement pe
    ON  pe.CollateralMasterId = m.Id
    AND pe.rn                 = 1

-- Representative building (rn=1) on the latest engagement
LEFT JOIN RepresentativeBuilding rb
    ON  rb.EngagementId = le.LatestEngagementId
    AND rb.rn           = 1

-- BuildingType description (EN) from the parameter table
LEFT JOIN parameter.Parameters bt
    ON  bt.[group]    = 'BuildingType'
    AND bt.[language] = 'EN'
    AND bt.[code]     = rb.BuildingTypeCode
    AND bt.[isactive] = 1

-- Type-specific detail rows (at most one per master)
LEFT JOIN collateral.LandDetails  ld ON ld.CollateralMasterId = m.Id
LEFT JOIN collateral.CondoDetails cd ON cd.CollateralMasterId = m.Id

WHERE m.IsDeleted = 0
  AND m.IsMaster  = 1
