-- CAS-AS400-Regulatory export view.
-- One record per appraisal application, represented by the LATEST appraisal of its chain.
--
-- SCOPE (confirmed by the business, 2026-08-14): report EVERYTHING we hold an appraisal for, not
-- only rows AS400 has already returned a collateral id for. An id arrives at drawdown, so gating on
-- it dropped every appraisal that had not reached drawdown yet — and dropped ALL of them wherever
-- the nightly COLLATLINK feed had not been ingested. Field 4 renders a missing id as zeros, which
-- is the agreed representation.
--
-- Block projects (PRJ) are the one exclusion — see the WHERE clause at the end of this file.
--
-- A chain is the set of appraisals linked by appraisal.Appraisals.PrevAppraisalId, which belongs to
-- a single customer by construction and is therefore the correct unit for this report. The previous
-- grouping by CollateralMasterId mixed several owners' histories into one row: the origination value
-- from a former owner could sit alongside the latest value from the current one.
-- Supplies all typed columns consumed by RegulatoryExportRow / RegulatoryFileWriter (300-char layout).
--
-- Engagement selection, per (chain, master):
--   Earliest: MIN(AppraisalDate) (tie-break CreatedAt ASC)  → origination values
--   Latest:   MAX(AppraisalDate) (tie-break CreatedAt DESC) → current values, and the record's identity
--
-- Representative building: Sequence=1 CollateralEngagementBuilding on the chain tip's engagement.
--   Used for BuildingTypeCode, BuildingArea, and parameter-table description lookup.
--
-- DOPA code: comes from request.RequestDetails.SubDistrict (the request's "Location" section), which
--   is DOPA-mastered. It is a 6-digit geocode, NOT the Thai name, and is matched against
--   parameter.DopaSubDistricts.Code (the PK) so an unknown/legacy value yields blank rather than a
--   truncated string in the 6-char field. Land/LB/LS*/Condo types only.
--   Do NOT switch this to LandDetails/CondoDetails.SubDistrict — those are DEED addresses on the
--   Title master, which no longer matches DOPA. See the field's own comment for the detail.
--
-- Numeric range guards (mirror CollateralResult LifeYear pattern — one bad value must not abort run):
--   ConstructionProgressPercent: out of [0,100]        → NULL (writer formats as 0.00)
--   LandAreaSqWa:                out of [0, 99999.99]  → NULL (dec(7,2) max in the 8-char field 151-158)
--   BuildingArea/UsableArea:     out of [0, 99999.99]  → NULL (dec(7,2) max in the 8-char field 159-166).
--                                For building types the guard applies to the TOTAL across every
--                                building, since two that each fit can still overflow together.
--   The guards are two-sided on purpose. A NEGATIVE area is just as fatal as an oversized one — the
--   writer multiplies by 100 and the minus sign consumes a character, so a single bad row threw
--   "Numeric field overflow: '-1025860' exceeds width 7" and aborted the WHOLE export, producing no
--   file at all (found on U3: appraisal 63A00115 carried LandArea = -10258.60).
--   BuildingAge / NumberOfFloors: handled in C# writer (clamped to [0,999])
--
-- Multi-building rule: building area is the SUM and building age the MAX over every row in
--   CollateralEngagementBuildings for the engagement. Building type and floor count still come from
--   the Sequence=1 building — neither combines across buildings. The same rule is applied by the
--   AS400 Collateral Result export (CollateralResultQuery.ToBuildingAge / ToAreaUtilization); keep
--   the two in step.
--
-- No JSON/snapshot column reads. GETDATE() only (application-locale time; never GETUTCDATE()).

CREATE OR ALTER VIEW collateral.vw_RegulatoryExport AS

WITH

-- ── Walk each engagement's chain back to its root ──────────────────────────────────────
-- The cycle guard is the Path string, NOT a depth limit. Never add a depth cap here: construction
-- inspections (purpose 06/11) easily push a chain past 20 links, and truncation would be silent —
-- the Nth ancestor would be returned as the chain root, sending a wrong value to the regulator.
--
-- Uses appraisal.Appraisals.PrevAppraisalId as the chain link (confirmed by the product owner).
--
-- NOTE: that column is written once at appraisal creation (Appraisal.cs:117) and is not re-synced
-- when the request is edited later. The team's chosen answer is to block editing PrevAppraisalId
-- once an appraisal exists, rather than to sync it. If editing is ever re-enabled, this view and
-- GetPreviousAppraisalChainQueryHandler must both be revisited together.
-- LEFT JOIN, not INNER, because of the AS400 legacy engagements. Collateral the bank has held since
-- before this system existed was valued inside AS400 and never appraised in CAS, so its engagement
-- carries a synthetic AppraisalId with no row in appraisal.Appraisals — deliberately, since minting
-- fake appraisals would surface them on every screen that lists appraisals. An INNER JOIN silently
-- dropped all 2,444 of them from the regulator's file.
--
-- Such an engagement has no PrevAppraisalId, so the recursive half below matches nothing and it
-- becomes the root of its own chain. That is correct: the AS400 valuation is the only one there is,
-- so it supplies both the origination and the current figures.
--
-- ACCEPTED LIMITATION: a collateral that has BOTH an old AS400 valuation and a newer CAS appraisal
-- produces TWO records — the legacy chain and the CAS chain — because nothing links them. 242 of
-- them on the production-like set. The business accepted this to get the legacy collateral reported
-- at all. Collapsing them would mean pointing the CAS chain root's PrevAppraisalId at the legacy
-- appraisal, i.e. rewriting real data.
ChainWalk AS (
    SELECT
        e.AppraisalId,
        ISNULL(a.Id, e.AppraisalId) AS AncestorId,
        a.PrevAppraisalId,
        0                 AS Depth,
        CAST('|' + CAST(ISNULL(a.Id, e.AppraisalId) AS varchar(36)) + '|' AS varchar(max)) AS Path
    FROM collateral.CollateralEngagements e
    LEFT JOIN appraisal.Appraisals a
        ON  a.Id = e.AppraisalId
    -- Keeps the old exclusion of soft-deleted appraisals while admitting the legacy engagements,
    -- which have no appraisal row at all.
    WHERE (a.Id IS NOT NULL AND a.IsDeleted = 0)
       OR  a.Id IS NULL

    UNION ALL

    SELECT
        c.AppraisalId,
        p.Id,
        p.PrevAppraisalId,
        c.Depth + 1,
        CAST(c.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
    FROM ChainWalk c
    JOIN appraisal.Appraisals p
        ON  p.Id        = c.PrevAppraisalId
        AND p.IsDeleted = 0
    WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', c.Path) = 0
),

-- The deepest ancestor reached is the chain root.
ChainRoot AS (
    SELECT
        AppraisalId,
        AncestorId AS RootAppraisalId,
        ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY Depth DESC) AS rn
    FROM ChainWalk
),

-- Each engagement paired with its chain root. RootAppraisalId is the grouping key rather than
-- CollateralMasterId alone: a chain is one customer's facility, the correct unit for this report.
EngagementWithRoot AS (
    SELECT
        e.Id                 AS EngagementId,
        e.CollateralMasterId,
        e.AppraisalId,
        e.AppraisalNumber,
        e.AppraisalType,
        e.AppraisalDate,
        e.AppraisalValue,
        e.AppraisalCompanyId,
        e.RequestId,
        e.CreatedAt,
        e.CurrentValue,
        e.IsUnderConstruction,
        e.ConstructionProgressPercent,
        cr.RootAppraisalId
    FROM collateral.CollateralEngagements e
    JOIN ChainRoot cr
        ON  cr.AppraisalId = e.AppraisalId
        AND cr.rn          = 1
),

-- IMPORTANT: every CTE below partitions by (RootAppraisalId, CollateralMasterId), not by
-- RootAppraisalId alone.
--
-- A single chain can span several masters — the customer substitutes collateral mid-facility, or a
-- dedup-key drift lands a reappraisal on a different master. Partitioning by RootAppraisalId alone
-- causes two problems:
--   1. the other masters in that chain vanish from the report even though the bank still holds them
--   2. the origination values are taken from a different master than the row reports on
-- Adding CollateralMasterId guarantees every value in a row describes the same physical collateral.

-- The latest appraisal of each (chain, master) represents the record.
ChainTip AS (
    SELECT
        r.*,
        ROW_NUMBER() OVER (
            PARTITION BY r.RootAppraisalId, r.CollateralMasterId
            ORDER BY r.AppraisalDate DESC, r.CreatedAt DESC
        ) AS rn
    FROM EngagementWithRoot r
),

-- The earliest appraisal of each (chain, master) supplies the origination values.
ChainEarliest AS (
    SELECT
        r.RootAppraisalId,
        r.CollateralMasterId,
        r.AppraisalDate  AS EarliestAppraisalDate,
        r.AppraisalValue AS EarliestAppraisalValue,
        ROW_NUMBER() OVER (
            PARTITION BY r.RootAppraisalId, r.CollateralMasterId
            ORDER BY r.AppraisalDate ASC, r.CreatedAt ASC
        ) AS rn
    FROM EngagementWithRoot r
),

-- The latest construction-inspection appraisal within this (chain, master).
ChainProgressive AS (
    SELECT
        r.RootAppraisalId,
        r.CollateralMasterId,
        r.AppraisalDate AS LatestProgressiveAppraisalDate,
        ROW_NUMBER() OVER (
            PARTITION BY r.RootAppraisalId, r.CollateralMasterId
            ORDER BY r.AppraisalDate DESC, r.CreatedAt DESC
        ) AS rn
    FROM EngagementWithRoot r
    WHERE r.AppraisalType = 'Progressive'
),

-- ── AS400 state for this (chain, master) ────────────────────────────────────────────────
-- The ChainHostLink CTE that used to sit here is gone. Its whole job was to answer "which
-- engagement of this (chain, master) speaks for the collateral right now" — picking the latest one
-- that actually carried an id, because inspections and revaluations involve no drawdown and so
-- become the chain tip with a NULL id. That question no longer exists: AS400's state now lives on
-- collateral.CollateralMasters, maintained by the nightly HOST_COLLATERAL_LINK feed, which is the
-- grain AS400 itself uses. Read m.HostCollateralId / m.IsRedeemed directly.

-- Representative building: Sequence=1 on the latest engagement.
-- Used ONLY for BuildingTypeCode and NumberOfFloors — neither of which can be meaningfully combined
-- across several buildings. Age and area come from BuildingAggregate below instead.
RepresentativeBuilding AS (
    SELECT
        ceb.EngagementId,
        ceb.BuildingTypeCode,
        ceb.NumberOfFloors,
        ROW_NUMBER() OVER (
            PARTITION BY ceb.EngagementId
            ORDER BY ceb.Sequence ASC
        ) AS rn
    FROM collateral.CollateralEngagementBuildings ceb
),

-- Every building on the engagement, combined: a title carrying a house plus an outbuilding must report
-- the whole footprint, and the age of the OLDEST structure is what drives the depreciation view.
-- Replaces the earlier Sequence=1 rule, which silently dropped the second and later buildings.
BuildingAggregate AS (
    SELECT
        ceb.EngagementId,
        -- Guard the TOTAL, not each building: two buildings that each fit can still overflow together.
        -- Two-sided: a negative total overflows the fixed-width field just as an oversized one does.
        CASE
            WHEN SUM(ceb.BuildingArea) NOT BETWEEN 0 AND 99999.99 THEN NULL
            ELSE SUM(ceb.BuildingArea)
        END              AS TotalBuildingArea,
        MAX(ceb.BuildingAge) AS MaxBuildingAge
    FROM collateral.CollateralEngagementBuildings ceb
    GROUP BY ceb.EngagementId
)

SELECT
    m.Id                                                        AS CollateralMasterId,
    m.CollateralType,

    -- Current AS400 state of the collateral itself, not of any one appraisal.
    m.HostCollateralId,

    -- Origination values come from the earliest appraisal in the same chain, not the master's
    -- oldest engagement, which may belong to a previous owner.
    ce.EarliestAppraisalDate,
    ce.EarliestAppraisalValue,

    -- The chain tip.
    t.AppraisalNumber                                            AS LatestAppraisalNumber,
    t.AppraisalType                                              AS LatestAppraisalType,
    t.AppraisalDate                                              AS LatestAppraisalDate,
    t.AppraisalValue                                             AS LatestAppraisalValue,

    -- Value as it stood at the chain tip's engagement, with part-built buildings counted at their
    -- construction progress rather than at 100%. Frozen by the Appraisal module's
    -- IConstructionCurrentValueService (the same code behind the Decision Summary construction card).
    -- NULL when that appraisal had no construction inspection — the writer then falls back to
    -- LatestAppraisalValue, because nothing was part-built.
    t.CurrentValue                                               AS CurrentValue,

    t.AppraisalCompanyId                                         AS LatestAppraisalCompanyId,

    -- Selling price (market price field): the request-level TotalSellingPrice of the chain tip's
    -- originating request (one row per request → deterministic).
    rd.TotalSellingPrice                                         AS SellingPrice,

    -- Latest Progressive engagement date within this chain.
    cp.LatestProgressiveAppraisalDate,

    -- Under-construction flag (drives field #5; the Y/N/L/blank string is formed in the writer)
    -- Field 5. From the chain tip's engagement, not LandDetails: that column read a single
    -- property's inspection (the primary is the LAND property while the inspection hangs off the
    -- BUILDING property), so it was 0 on every dev row even where buildings were part-built — and
    -- the progress CASE below then reported those as 100% complete.
    ISNULL(t.IsUnderConstruction, 0)                 AS IsUnderConstruction,

    -- Construction progress % (field #6) — the full regulatory rule is computed HERE so the fixed-width
    -- and Excel writers only format the value (single source of truth, no duplication):
    --   not real estate (machinery, …) → 0; bare land (L / Leasehold land LSL) → 0;
    --   everything with a structure (LB/LSB/LS, condo U/LSU, legacy UNK):
    --     completed (not under construction) → 100, under construction → progress%
    --     (guarded to 0–100; 0 when none recorded).
    --
    -- Condo and UNK are in the allow-list on purpose: the business rule is "every REAL-ESTATE
    -- collateral", and condo was the only real-estate type being reported as 0% built. The bank's own
    -- 2026-08-02 file agrees — it sends N + 100.00% for all 7,716 condo and all 1,209 legacy rows.
    -- Bare land sends 0% there too (1,112 of 1,155 rows), which is why L / LSL is 0 and not 100.
    CASE
        WHEN m.CollateralType NOT IN ('L', 'LB', 'LSL', 'LSB', 'LS', 'U', 'LSU', 'UNK') THEN 0
        WHEN m.CollateralType IN ('L', 'LSL')                            THEN 0
        WHEN ISNULL(t.IsUnderConstruction, 0) = 0                        THEN 100
        WHEN t.ConstructionProgressPercent BETWEEN 0 AND 100             THEN t.ConstructionProgressPercent
        ELSE 0
    END                                                           AS ConstructionProgressPercent,

    -- Land area (sq.wa): Land/LB/LS* types. Two-sided guard on [0, 99999.99] — anything outside it
    -- overflows the dec(7,2) field, and a negative value used to abort the entire export.
    CASE
        WHEN ld.LandArea NOT BETWEEN 0 AND 99999.99 THEN NULL
        ELSE ld.LandArea
    END                                                           AS LandAreaSqWa,

    -- Number of floors: building types only (LB/LSB/LS), from the representative engagement building.
    --   • Condo (U / LSU) / bare land / machinery: NULL → writer renders 0 (spec "else 0").
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN CAST(rb.NumberOfFloors AS int)
        ELSE NULL
    END                                                           AS NumberOfFloors,

    -- Building age (years): all building types + condo.
    --   • Building/L&B (LB, LSB, LS): age of the OLDEST building on the engagement
    --   • Condo (U) and leasehold condo (LSU): BuildingAge from CondoDetails
    --   • Others (bare land, machinery): NULL
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN ba.MaxBuildingAge
        WHEN m.CollateralType IN ('U', 'LSU')        THEN cd.BuildingAge
        ELSE NULL
    END                                                           AS BuildingAge,

    -- Building area (area utilization):
    --   • Building/L&B (LB, LSB, LS): TOTAL area of every building on the engagement
    --   • Condo (U) and leasehold condo (LSU): UsableArea from CondoDetails
    --   • Others: NULL
    CASE
        WHEN m.CollateralType IN ('LB', 'LSB', 'LS') THEN ba.TotalBuildingArea       -- already guarded to [0, 99999.99] in CTE
        WHEN m.CollateralType IN ('U', 'LSU') AND cd.UsableArea BETWEEN 0 AND 99999.99 THEN cd.UsableArea -- guard the condo path too (dec(7,2))
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

    -- DOPA 6-digit sub-district code — an ADMINISTRATIVE address, so it has to come from a
    -- DOPA-mastered source. That is request.RequestDetails: the request's "Location" section, whose
    -- picker uses the DOPA master.
    --
    -- NOT LandDetails/CondoDetails.SubDistrict: those hold the DEED address, mastered by
    -- parameter.TitleSubDistricts, which DIVERGED from DOPA on 2026-07-29 (11,144 vs 7,436 codes;
    -- 3,715 Title codes exist in no DOPA table). Reading a deed code here silently blanks every one of
    -- those, and the two addresses genuinely differ — they are not copies of each other.
    --
    -- Still validated against parameter.DopaSubDistricts.Code (the PK) so an unknown value yields blank
    -- rather than a truncated string in the 6-char field. Land/LB/LS*/Condo types only.
    --
    -- LIMITATION: RequestDetails is request-level, so every collateral on one request reports the same
    -- sub-district. The per-property source is appraisal.{Land,Condo}AppraisalDetails.DopaSubDistrict
    -- (added by 7f366e4b and already carried on AppraisalForCollateralResult), but it is populated on
    -- 1 of 105,469 rows today against 99.99% coverage here. Switch once it has been backfilled — that
    -- also needs the column carried onto collateral.LandDetails / CondoDetails, which do not have it.
    CASE
        WHEN m.CollateralType IN ('L', 'LB', 'LSL', 'LSB', 'LS', 'U', 'LSU')
            THEN (
                SELECT dsd.Code
                FROM parameter.DopaSubDistricts dsd
                WHERE dsd.Code = rd.SubDistrict
            )
        ELSE NULL
    END                                                           AS DopaCode

-- The view is driven by the latest appraisal of each chain, not by the master.
FROM ChainTip t

-- The master supplies descriptive attributes only — it is neither the record's unit nor the home
-- of any AS400 state.
JOIN collateral.CollateralMasters m
    ON  m.Id = t.CollateralMasterId

-- Earliest appraisal of the (chain, master) (rn=1).
LEFT JOIN ChainEarliest ce
    ON  ce.RootAppraisalId    = t.RootAppraisalId
    AND ce.CollateralMasterId = t.CollateralMasterId
    AND ce.rn                 = 1

-- Request detail of the chain tip → selling price (one row per request).
LEFT JOIN request.RequestDetails rd
    ON  rd.RequestId = t.RequestId

-- Latest construction inspection within the (chain, master) (rn=1).
LEFT JOIN ChainProgressive cp
    ON  cp.RootAppraisalId    = t.RootAppraisalId
    AND cp.CollateralMasterId = t.CollateralMasterId
    AND cp.rn                 = 1

-- Representative building (rn=1) on the chain tip's engagement — building type and floors only.
LEFT JOIN RepresentativeBuilding rb
    ON  rb.EngagementId = t.EngagementId
    AND rb.rn           = 1

-- All buildings on the chain tip's engagement, combined — supplies age and area.
LEFT JOIN BuildingAggregate ba
    ON  ba.EngagementId = t.EngagementId

-- BuildingType description (EN) from the parameter table
LEFT JOIN parameter.Parameters bt
    ON  bt.[group]    = 'BuildingType'
    AND bt.[language] = 'EN'
    AND bt.[code]     = rb.BuildingTypeCode
    AND bt.[isactive] = 1

-- Type-specific detail rows (at most one per master).
--
-- A leasehold master (LSL / LSB / LS / LSU) carries ONLY collateral.LeaseholdDetails: every physical
-- attribute — land area, condo usable area, building age — lives on the UNDERLYING real-estate
-- master it points at, because one appraisal property produces two rows (the RE row and the lease
-- row, the lease being the one that owns the engagement).
--
-- Joining these on m.Id alone therefore sent every leasehold record out with zeros in fields 19/20,
-- which contradicts the spec: "Land types" = L, LB, LSL, LSB, LS and "Building types" = LB, LSB, LS
-- both include leasehold codes (docs/regulatory/regulatory-field-reference.md).
-- Redirect to the underlying master for leasehold rows; every other type resolves to itself.
LEFT JOIN collateral.LeaseholdDetails lhd_attr ON lhd_attr.CollateralMasterId = m.Id
LEFT JOIN collateral.LandDetails  ld ON ld.CollateralMasterId = COALESCE(lhd_attr.UnderlyingMasterId, m.Id)
LEFT JOIN collateral.CondoDetails cd ON cd.CollateralMasterId = COALESCE(lhd_attr.UnderlyingMasterId, m.Id)

-- Filter
--   t.rn = 1                       → the latest appraisal of each (chain, master) only
--   m.IsDeleted = 0 / IsMaster = 1 → belt and braces (engagements always attach to IsMaster rows)
--   CollateralType IN (...)        → real estate only, as an ALLOW-list
--   IsRedeemed = 0                 → drop only what AS400 has explicitly reported as redeemed
--
-- IsRedeemed is a NOT NULL bit defaulting to 0, so collateral AS400 has said nothing about yet stays
-- in the report. That is deliberate: "no news" is not the same as "released", and excluding it would
-- hide collateral the bank holds.
--
-- The type filter is an ALLOW-list, not a list of exclusions. The report covers real estate, and an
-- exclusion list only holds while nobody adds a type code: it read `<> 'PRJ'` before, which looked
-- correct purely because the data happened to carry no machinery. The moment a MAC master exists it
-- would have gone to the regulator unannounced. Anything new must now be added here on purpose.
--
--   L / LB / U      — land, land-and-building, condo unit
--   LSL/LSB/LS/LSU  — leasehold over land or a condo; still real estate (product owner, 16 Aug)
--   UNK             — collateral carried over from AS400 with no identifying data. Reported on the
--                     strength of the source: every legacy row carries construction status, which
--                     only means anything for a building. Its location, land area, building age and
--                     usable area go out blank — the listing has no such data and there is no other
--                     source. Accepted by the business.
--
-- Deliberately absent: MAC (machinery is not real estate) and PRJ (block projects are out of scope —
-- AS400 issues one collateral id per financed unit, so a project would be one hollow row with its
-- construction, floors, age, area and building-type fields already blanked by the gates above).
WHERE t.rn              = 1
  AND m.IsDeleted       = 0
  AND m.IsMaster        = 1
  AND m.CollateralType IN ('L', 'LB', 'U', 'LSL', 'LSB', 'LS', 'LSU', 'UNK')
  AND m.IsRedeemed      = 0
