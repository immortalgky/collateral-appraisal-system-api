-- CAS-AS400-Regulatory export view, VERSION 2.
--
-- One record per APPRAISAL CHAIN, represented by the latest appraisal in it, plus one record per
-- legacy AS400 listing row that no chain has taken over.
--
-- ── Why a second view rather than a change to the first ────────────────────────────────────────
-- v1 (vw_RegulatoryExport) is driven by collateral.CollateralEngagements, which only exist once a
-- CollateralMaster has been created. On the production-like dataset 6,699 completed appraisals never
-- get one — condo rows missing SubDistrict (4,195), land with no title number (553), leaseholds that
-- never resolve an underlying master (collateral.LeaseholdDetails is empty), titles that span two
-- masters (17). Those appraisals simply never reach the regulator.
--
-- The report does not actually need the master. Its unit is "the collateral as most recently
-- appraised, with the date it was first appraised", and both come from walking
-- appraisal.Appraisals.PrevAppraisalId. Measured against the bank's own 2026-08-02 file, this view's
-- grain closes the master-creation gap entirely (5,637 appraisals recovered) while matching the
-- bank's values as well as v1 does: valuation date 99.99%, price 99.96%, construction status 99.61%.
--
-- v1 stays in place and unchanged. Both are wired to their own recurring job so the two files can be
-- produced from the same data and compared before the switch; regulatory-export-v2 ships disabled.
--
-- ── What the master still owns ─────────────────────────────────────────────────────────────────
-- CollateralMaster is NOT going away: COLLATERAL_RESULT (the 208-char outbound), the collateral
-- catalog screens, the alias hierarchy and block reappraisal all key off it. Only this report stops
-- depending on it.
--
-- ── Grain ──────────────────────────────────────────────────────────────────────────────────────
-- Every appraisal that reaches the same chain root collapses to ONE row. A chain of six progressive
-- inspections is one record, reporting the sixth appraisal's figures and the first one's date.
--
-- "Chain root" is NOT the oldest reachable ancestor. The walk stops where the history branches —
-- see BranchPoint below — because an appraisal that several later appraisals continue from is shared
-- history, not one collateral. Walking past it merges unrelated parcels into a single row.
--
-- ── Deliberately excluded ──────────────────────────────────────────────────────────────────────
--   Block projects (PRJ) — AS400 mints one collateral id per financed UNIT and the COLLATLINK file
--     carries no unit key, so we cannot tell which units have been redeemed. Reporting a released
--     unit to the regulator is worse than reporting nothing, so projects wait for AS400 to supply
--     the unit key. 44,498 units sit behind this.
--   Machinery-only and property-less appraisals — not real estate.
--   Anything AS400 has explicitly reported as redeemed ('R' in the nightly feed).
--
-- ── Numeric range guards ───────────────────────────────────────────────────────────────────────
-- Same two-sided rule as v1: a value outside [0, 99999.99] overflows the fixed-width field, and a
-- NEGATIVE one is as fatal as an oversized one because the writer multiplies by 100 and the minus
-- sign consumes a character. One bad row used to abort the entire file.
--
-- OPTION (MAXRECURSION 0) cannot live inside a view — the caller adds it. See
-- RegulatoryExportV2Query. Without it a chain longer than 100 aborts the whole query.
CREATE OR ALTER VIEW collateral.vw_RegulatoryExportV2
AS
WITH

-- ── Appraisals that more than one later appraisal continues from ───────────────────────────────
-- PrevAppraisalId carries two different meanings and only the shape of the graph tells them apart.
-- Where a parent has ONE child, the child re-values what the parent valued — same collateral, one
-- history, one row. Where a parent has SEVERAL, they cannot all be the same collateral: 63A02257
-- (a plain 'New', not a project) has three children, and two of them carry different title deeds —
-- 64A01549 on 6271, 64A01550 on 6273. Two parcels. Collapsing them to their shared parent reports
-- one row where the bank holds two, and the bank's own file agrees: it lists both, each as its own
-- Newest Application Id.
--
-- So the walk stops climbing at a branch: a child of a multi-child parent IS its own chain root.
-- The parent's own row still reports separately if it qualifies. On the production-like set this
-- recovers 238 appraisals that the bank reports and we did not.
--
-- Children that are block projects are excluded from the count. They never climb (see the project
-- dead-end test below), so counting them would stop a sibling that has no real branch to worry
-- about.
BranchPoint AS (
    SELECT c.PrevAppraisalId AS AppraisalId
    FROM appraisal.Appraisals c
    WHERE c.IsDeleted        = 0
      AND c.Status           = 'Completed'
      AND c.PrevAppraisalId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = c.Id)
    GROUP BY c.PrevAppraisalId
    HAVING COUNT(*) > 1
),

-- ── Walk every completed appraisal back to its chain root ──────────────────────────────────────
-- The cycle guard is the Path CHARINDEX test, NOT a depth limit. A depth predicate would stop the
-- recursion before MAXRECURSION could fire, silently truncating long chains and returning the Nth
-- ancestor as if it were the root. Construction-inspection chains reach dozens of links in practice.
ChainWalk AS (
    SELECT
        a.Id                AS AppraisalId,
        a.Id                AS AncestorId,
        a.PrevAppraisalId,
        0                   AS Depth,
        CAST('|' + CAST(a.Id AS varchar(36)) + '|' AS varchar(max)) AS Path
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
      AND a.Status    = 'Completed'

    UNION ALL

    SELECT
        w.AppraisalId,
        p.Id,
        p.PrevAppraisalId,
        w.Depth + 1,
        CAST(w.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
    FROM ChainWalk w
    JOIN appraisal.Appraisals p
        ON  p.Id        = w.PrevAppraisalId
        AND p.IsDeleted = 0
    WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', w.Path) = 0
      -- A block-project appraisal is not a continuation of one physical collateral's history:
      -- many unrelated units can point PrevAppraisalId at the SAME project (fan-in, up to 65 seen
      -- on the production-like set), and a project's own PrevAppraisalId can point back at a unit
      -- it absorbed. Either direction would merge unrelated collateral into one chain, so a
      -- project is a dead end both ways — never added as anyone's ancestor, never a bridge back.
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = p.Id)
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = w.AncestorId)
      -- Stop at a branch. The test is on the node being ENTERED (p), not the one the walk stands on:
      -- refusing to enter a multi-child parent leaves w.AncestorId as the root, which is exactly the
      -- child that must become its own chain. Testing w.AncestorId instead would let the walk step
      -- INTO the branch point first and merge the siblings there anyway.
      AND NOT EXISTS (SELECT 1 FROM BranchPoint bp WHERE bp.AppraisalId = p.Id)
),

ChainRoot AS (
    SELECT AppraisalId, AncestorId AS RootAppraisalId
    FROM (
        SELECT AppraisalId, AncestorId,
               ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY Depth DESC) AS rn
        FROM ChainWalk
    ) z
    WHERE rn = 1
),

-- ValuationAnalyses is 1:1 with Appraisal (unique index on AppraisalId) — no fan-out.
Val AS (
    SELECT v.AppraisalId, v.ValuationDate, v.AppraisedValue, v.ForcedSaleValue
    FROM appraisal.ValuationAnalyses v
),

-- ── Construction status per appraisal ──────────────────────────────────────────────────────────
-- Mirrors IConstructionCurrentValueService.CiAggregateSql exactly, so the screen and this file
-- cannot disagree. A summary inspection carries its own percentage; a full-detail one is the sum of
-- its work rows.
Ci AS (
    SELECT
        ap.AppraisalId,
        ISNULL(SUM(ci.TotalValue), 0) AS TotalValue,
        ISNULL(SUM(
            CASE WHEN ci.IsFullDetail = 0
                 THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                 ELSE ISNULL(wd.CurrentSum, 0)
            END), 0) AS CurrentValue
    FROM appraisal.ConstructionInspections ci
    JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
    LEFT JOIN (
        SELECT ConstructionInspectionId, SUM(CurrentPropertyValue) AS CurrentSum
        FROM appraisal.ConstructionWorkDetails
        GROUP BY ConstructionInspectionId
    ) wd ON wd.ConstructionInspectionId = ci.Id
    GROUP BY ap.AppraisalId
),

-- ── What each appraisal is made of ─────────────────────────────────────────────────────────────
-- The collateral type is DERIVED from the property mix rather than read from a master row, which is
-- the whole point of this view. Land wins over everything (the product rule v1 also follows);
-- leasehold codes keep their own identity.
PropMix AS (
    SELECT
        p.AppraisalId,
        MAX(CASE WHEN p.PropertyType IN ('L', 'LB')  THEN 1 ELSE 0 END) AS HasLand,
        -- 'LB' is a single property that IS land-and-building — it carries BOTH a
        -- LandAppraisalDetails and a BuildingAppraisalDetails row (all 38,907 of them do). A separate
        -- 'B' row is the exception, not the rule: only 95 appraisals carry both. Treating 'B' alone
        -- as the building signal typed 34,077 land-and-building records as bare land.
        MAX(CASE WHEN p.PropertyType IN ('B', 'LB')  THEN 1 ELSE 0 END) AS HasBuilding,
        MAX(CASE WHEN p.PropertyType = 'U'           THEN 1 ELSE 0 END) AS HasCondo,
        MAX(CASE WHEN p.PropertyType = 'LSL'         THEN 1 ELSE 0 END) AS HasLeaseLand,
        MAX(CASE WHEN p.PropertyType = 'LSB'         THEN 1 ELSE 0 END) AS HasLeaseBuilding,
        MAX(CASE WHEN p.PropertyType = 'LS'          THEN 1 ELSE 0 END) AS HasLeaseBoth,
        MAX(CASE WHEN p.PropertyType = 'LSU'         THEN 1 ELSE 0 END) AS HasLeaseCondo,
        MAX(CASE WHEN p.PropertyType IN ('L','LB','U','LSL','LSB','LS','LSU','B') THEN 1 ELSE 0 END) AS HasRealEstate
    FROM appraisal.AppraisalProperties p
    GROUP BY p.AppraisalId
),

-- Land area (sq.wa) summed across every title of every land property on the appraisal. Rai and Ngan
-- are converted the same way LandArea.TotalSquareWa does: 1 rai = 400 sq.wa, 1 ngan = 100 sq.wa.
LandAgg AS (
    SELECT
        p.AppraisalId,
        SUM(ISNULL(t.AreaRai, 0) * 400 + ISNULL(t.AreaNgan, 0) * 100 + ISNULL(t.AreaSquareWa, 0)) AS LandAreaSqWa
    FROM appraisal.AppraisalProperties p
    JOIN appraisal.LandAppraisalDetails d ON d.AppraisalPropertyId = p.Id
    JOIN appraisal.LandTitles t           ON t.LandAppraisalDetailId = d.Id
    WHERE p.PropertyType IN ('L', 'LB', 'LSL', 'LS')
    GROUP BY p.AppraisalId
),

-- Buildings combined: the OLDEST age and the TOTAL area across every building, matching the rule the
-- AS400 result interface uses. A single building cannot speak for a plot that holds several.
BuildingAgg AS (
    SELECT
        p.AppraisalId,
        MAX(b.BuildingAge)        AS MaxBuildingAge,
        SUM(b.TotalBuildingArea)  AS TotalBuildingArea
    FROM appraisal.AppraisalProperties p
    JOIN appraisal.BuildingAppraisalDetails b ON b.AppraisalPropertyId = p.Id
    WHERE p.PropertyType IN ('B', 'LB', 'LSB', 'LS')
    GROUP BY p.AppraisalId
),

-- Representative building for the fields that cannot be combined across several buildings.
RepBuilding AS (
    SELECT AppraisalId, BuildingType, NumberOfFloors
    FROM (
        SELECT p.AppraisalId, b.BuildingType, b.NumberOfFloors,
               ROW_NUMBER() OVER (
                   PARTITION BY p.AppraisalId
                   -- A typed building first, so the export's representative carries a type at all.
                   ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(b.BuildingType, ''))), '') IS NULL
                                 THEN 1 ELSE 0 END,
                            p.SequenceNumber
               ) AS rn
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.BuildingAppraisalDetails b ON b.AppraisalPropertyId = p.Id
        WHERE p.PropertyType IN ('B', 'LB', 'LSB', 'LS')
    ) z
    WHERE rn = 1
),

-- Condo unit attributes (one condo property per appraisal in practice; take the first by sequence).
CondoAgg AS (
    SELECT AppraisalId, UsableArea, BuildingAge
    FROM (
        SELECT p.AppraisalId, c.UsableArea, c.BuildingAge,
               ROW_NUMBER() OVER (PARTITION BY p.AppraisalId ORDER BY p.SequenceNumber) AS rn
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.CondoAppraisalDetails c ON c.AppraisalPropertyId = p.Id
        WHERE p.PropertyType IN ('U', 'LSU')
    ) z
    WHERE rn = 1
),

-- ── Every node of every chain, with the numbers needed to pick its ends ────────────────────────
Node AS (
    SELECT
        cr.RootAppraisalId,
        a.Id                AS AppraisalId,
        a.AppraisalNumber,
        a.AppraisalType,
        a.RequestId,
        v.ValuationDate,
        v.AppraisedValue,
        ci.TotalValue       AS CiTotal,
        ci.CurrentValue     AS CiCurrent,
        ROW_NUMBER() OVER (PARTITION BY cr.RootAppraisalId
                           ORDER BY v.ValuationDate DESC, a.AppraisalNumber DESC) AS rnLatest,
        ROW_NUMBER() OVER (PARTITION BY cr.RootAppraisalId
                           ORDER BY v.ValuationDate ASC,  a.AppraisalNumber ASC)  AS rnEarliest,
        ROW_NUMBER() OVER (PARTITION BY cr.RootAppraisalId
                           ORDER BY CASE WHEN a.AppraisalType = 'Progressive' THEN 0 ELSE 1 END,
                                    v.ValuationDate DESC) AS rnProgressive
    FROM ChainRoot cr
    JOIN appraisal.Appraisals a ON a.Id = cr.AppraisalId
    LEFT JOIN Val v  ON v.AppraisalId  = a.Id
    LEFT JOIN Ci  ci ON ci.AppraisalId = a.Id
),

-- ── Which node of each chain the physical characteristics are read from ────────────────────────
-- Physical characteristics (land/building/condo type, area, building age) don't meaningfully change
-- between rounds of the same collateral. A Progressive/PreAppraisal round often records no
-- AppraisalProperties of its own (it just tracks construction progress on top of an earlier survey),
-- so the source is the LATEST node in the chain that actually has a property row, not the tip.
--
-- This MUST be a CTE joined once, never an OUTER APPLY over Node. SQL Server does not materialise a
-- CTE: an APPLY correlated on t.RootAppraisalId re-runs Node — and therefore the whole recursive
-- ChainWalk — once per OUTPUT ROW. At 49,883 rows that took the view from 7 seconds to 108.
PropSource AS (
    SELECT RootAppraisalId, AppraisalId AS PropSourceAppraisalId
    FROM (
        SELECT n2.RootAppraisalId, n2.AppraisalId,
               -- AppraisalNumber breaks ties; without it two nodes sharing a ValuationDate would
               -- hand the report a different set of physical characteristics from run to run.
               ROW_NUMBER() OVER (PARTITION BY n2.RootAppraisalId
                                  ORDER BY n2.ValuationDate DESC, n2.AppraisalNumber DESC) AS rn
        FROM Node n2
        WHERE EXISTS (SELECT 1 FROM appraisal.AppraisalProperties p
                      WHERE p.AppraisalId = n2.AppraisalId)
    ) z
    WHERE rn = 1
),

-- ── Chains that reach the file ─────────────────────────────────────────────────────────────────
-- The same admission test SOURCE 1 applies to itself, expressed once so other rules can ask "does
-- this chain actually get reported?" without re-deriving it. Keep the predicates below in step with
-- SOURCE 1's WHERE clause — the branch-point rule that consumes this is only sound while they agree.
ReportableChain AS (
    SELECT n.RootAppraisalId
    FROM Node n
    JOIN PropSource ps ON ps.RootAppraisalId = n.RootAppraisalId
    JOIN PropMix pm    ON pm.AppraisalId     = ps.PropSourceAppraisalId
    LEFT JOIN collateral.HostCollateralLinks h ON h.AppraisalNumber = n.AppraisalNumber
    WHERE n.rnLatest = 1
      AND pm.HasRealEstate = 1
      AND ISNULL(h.IsRedeemed, 0) = 0
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = n.AppraisalId)
),

-- ── Branch points whose successors already cover them ──────────────────────────────────────────
-- Given A ← B and A ← C, the file must carry B and C but NOT A: the branch rule already split B and
-- C into chains of their own, and A's collateral lives on in both. Reporting A as well is the same
-- physical collateral a third time. The bank's own file agrees on 99 of these — it lists A but points
-- its Newest Application Id at a later appraisal.
--
-- The guard is the join to ReportableChain, and it is not optional: where every successor drops out
-- of the file (no property rows of its own, redeemed, superseded again further down) A is the only
-- row that collateral has left, and removing it would delete it from the report entirely. 5 chains on
-- the production-like set are in exactly that position and must keep reporting A.
--
-- Computed as one uncorrelated set on purpose. Written as a correlated EXISTS inside SOURCE 1's WHERE
-- it would re-run Node — and the whole recursive walk behind it — once per output row, the same trap
-- that PropSource was extracted from.
SupersededBranch AS (
    SELECT DISTINCT kid.PrevAppraisalId AS AppraisalId
    FROM appraisal.Appraisals kid
    JOIN BranchPoint bp      ON bp.AppraisalId       = kid.PrevAppraisalId
    JOIN ReportableChain rc  ON rc.RootAppraisalId   = kid.Id
    WHERE kid.IsDeleted = 0
      AND kid.Status    = 'Completed'
),

-- ── AS400 legacy listing (the "99" series) ────────────────────────────────────────────────────
-- Collateral the bank holds that was never appraised in CAS, so no chain can reach it. The listing
-- is the bank's own migrated regulatory data and is already close to a finished record.
--
-- A listing row is dropped when a real appraisal has taken over the same AS400 collateral id — the
-- chain reports it instead, and emitting both would double-count one physical collateral. 266 rows
-- fall out this way.
Legacy AS (
    SELECT
        LTRIM(RTRIM(l.ApplicationId))            AS AppraisalNumber,
        CAST(CAST(l.CollateralID AS bigint) AS varchar(19)) AS HostCollateralId,
        l.ValuationDate,
        l.ValuationPriceInBaht,
        l.AppraisalValueAsCompleted,
        l.UnderConstruction,
        l.ProcessOfConstruction
    FROM appraisal.AS400ReportListing l
),

-- The legacy listing's OLDEST valuation date per AS400 collateral id. Aggregated before it is
-- joined, because the listing is NOT unique on CollateralID — two rows can share one id (68A09002
-- hit this and came out as two identical records). Joining the rows themselves fans the chain out.
LegacyEarliest AS (
    SELECT HostCollateralId, MIN(ValuationDate) AS ValuationDate
    FROM Legacy
    GROUP BY HostCollateralId
),

-- Appraisal numbers that a chain already reports, by AS400 collateral id.
LiveHostIds AS (
    SELECT DISTINCT h.HostCollateralId
    FROM collateral.HostCollateralLinks h
    JOIN Node n ON n.AppraisalNumber = h.AppraisalNumber AND n.rnLatest = 1
    WHERE h.HostCollateralId IS NOT NULL
      AND h.IsRedeemed = 0
)

-- ═══════════════════════════ SOURCE 1 — appraisal chains ═══════════════════════════
SELECT
    t.AppraisalNumber                                            AS LatestAppraisalNumber,

    -- Derived from the property mix. Land is unconditionally the primary when present; the leasehold
    -- codes take precedence over their freehold equivalents because a leasehold IS what is pledged.
    CASE
        WHEN pm.HasLeaseBoth     = 1 THEN 'LS'
        WHEN pm.HasLeaseBuilding = 1 THEN 'LSB'
        -- Leased land with a building standing on it is LS, not LSL. LSL means bare leased land, and
        -- RegulatoryFileWriter sends 'L' — "no structure" — for L and LSL alike. So an LSL row that
        -- carries buildings tells the regulator the plot is empty. 60A01460 (B,B,LSL,B — three
        -- buildings) and 65A00530 hit this; the bank's own file sends N for both. 45 of the 52 LSL
        -- rows on the production-like set have buildings.
        WHEN pm.HasLeaseLand     = 1 AND pm.HasBuilding = 1 THEN 'LS'
        WHEN pm.HasLeaseLand     = 1 THEN 'LSL'
        WHEN pm.HasLeaseCondo    = 1 THEN 'LSU'
        WHEN pm.HasLand = 1 AND pm.HasBuilding = 1 THEN 'LB'
        WHEN pm.HasLand = 1 THEN 'L'
        WHEN pm.HasCondo = 1 THEN 'U'
        -- Building-only: a construction inspection recorded without its land. It belongs to a chain
        -- whose earlier appraisals carried the land, so it reports as land-and-building.
        WHEN pm.HasBuilding = 1 THEN 'LB'
        ELSE 'L'
    END                                                          AS CollateralType,

    h.HostCollateralId,
    t.AppraisalType                                              AS LatestAppraisalType,

    -- Field 5. Value-weighted across every inspected building on the latest appraisal.
    CASE WHEN ISNULL(t.CiTotal, 0) > 0 AND t.CiCurrent < t.CiTotal THEN CAST(1 AS bit)
         ELSE CAST(0 AS bit) END                                 AS IsUnderConstruction,

    -- Field 6 — the full regulatory rule is computed HERE so the writers only format it:
    --   not real estate → 0; bare land (L / LSL) → 0; everything with a structure → 100 when
    --   complete, else the progress percentage. Condo and the legacy series count as structures:
    --   the bank's own file sends N + 100.00% for both.
    CASE
        WHEN pm.HasLand = 1 AND pm.HasBuilding = 0 AND pm.HasCondo = 0
             AND pm.HasLeaseBuilding = 0 AND pm.HasLeaseBoth = 0                     THEN 0
        WHEN pm.HasLeaseLand = 1 AND pm.HasLeaseBuilding = 0 AND pm.HasLeaseBoth = 0
             AND pm.HasBuilding = 0                                                  THEN 0
        WHEN ISNULL(t.CiTotal, 0) = 0                                                THEN 100
        WHEN t.CiCurrent >= t.CiTotal                                                THEN 100
        ELSE CAST(t.CiCurrent / t.CiTotal * 100 AS decimal(5, 2))
    END                                                          AS ConstructionProgressPercent,

    t.AppraisedValue                                             AS LatestAppraisalValue,
    e.AppraisedValue                                             AS EarliestAppraisalValue,

    -- Field 7 — value as it stands today. NULL when nothing is part-built, and the writer then falls
    -- back to the as-completed value.
    CASE WHEN ISNULL(t.CiTotal, 0) > 0 AND t.CiCurrent < t.CiTotal
         THEN t.CiCurrent ELSE NULL END                          AS CurrentValue,

    rd.TotalSellingPrice                                         AS SellingPrice,

    CASE WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1
         THEN CAST(rb.NumberOfFloors AS int) ELSE NULL END       AS NumberOfFloors,

    CASE
        WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1 THEN ba.MaxBuildingAge
        WHEN pm.HasCondo = 1 OR pm.HasLeaseCondo = 1                              THEN ca.BuildingAge
        ELSE NULL
    END                                                          AS BuildingAge,

    t.ValuationDate                                              AS LatestAppraisalDate,
    pg.ValuationDate                                             AS LatestProgressiveAppraisalDate,

    -- The chain's own first appraisal, or the legacy listing's date when AS400 valued this collateral
    -- before CAS existed. That legacy date is older by definition and is the true origination date —
    -- the reason the legacy rows were once imported as a chain's first engagement.
    CASE WHEN lg.ValuationDate IS NOT NULL AND lg.ValuationDate < e.ValuationDate
         THEN lg.ValuationDate ELSE e.ValuationDate END          AS EarliestAppraisalDate,

    asg.AppraisalCompanyId                                       AS LatestAppraisalCompanyId,

    -- DOPA 6-digit sub-district. Administrative address, so it must come from a DOPA-mastered source:
    -- request.RequestDetails, whose picker uses the DOPA master. NOT the deed address, which is
    -- mastered by parameter.TitleSubDistricts and has diverged from DOPA.
    CASE WHEN pm.HasRealEstate = 1
         THEN (SELECT dsd.Code FROM parameter.DopaSubDistricts dsd
               WHERE dsd.Code = LTRIM(RTRIM(rd.SubDistrict)))
         ELSE NULL END                                           AS DopaCode,

    CASE WHEN la.LandAreaSqWa NOT BETWEEN 0 AND 99999.99 THEN NULL
         ELSE la.LandAreaSqWa END                                AS LandAreaSqWa,

    CASE
        WHEN (pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1)
             AND ba.TotalBuildingArea BETWEEN 0 AND 99999.99 THEN ba.TotalBuildingArea
        WHEN (pm.HasCondo = 1 OR pm.HasLeaseCondo = 1)
             AND ca.UsableArea BETWEEN 0 AND 99999.99        THEN ca.UsableArea
        ELSE NULL
    END                                                          AS BuildingArea,

    CASE WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1
         THEN rb.BuildingType ELSE NULL END                      AS BuildingTypeCode,

    CASE WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1
         THEN bt.[description] ELSE NULL END                     AS BuildingTypeDescription

FROM Node t
JOIN Node e
    ON  e.RootAppraisalId = t.RootAppraisalId
    AND e.rnEarliest      = 1
LEFT JOIN Node pg
    ON  pg.RootAppraisalId = t.RootAppraisalId
    AND pg.rnProgressive   = 1
    AND pg.AppraisalType   = 'Progressive'
-- Where the physical characteristics come from — see the PropSource CTE. No match (no node in the
-- whole chain carries a property row) still fails the PropMix join below exactly as before.
LEFT JOIN PropSource ps ON ps.RootAppraisalId = t.RootAppraisalId
JOIN PropMix pm            ON pm.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN LandAgg la       ON la.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN BuildingAgg ba   ON ba.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN RepBuilding rb   ON rb.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN CondoAgg ca      ON ca.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN request.RequestDetails rd ON rd.RequestId = t.RequestId
LEFT JOIN collateral.HostCollateralLinks h ON h.AppraisalNumber = t.AppraisalNumber
LEFT JOIN LegacyEarliest lg ON lg.HostCollateralId = h.HostCollateralId
LEFT JOIN parameter.Parameters bt
    ON  bt.[group]    = 'BuildingType'
    AND bt.[language] = 'EN'
    AND bt.[code]     = rb.BuildingType
    AND bt.[isactive] = 1
OUTER APPLY (
    SELECT TOP 1 TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier) AS AppraisalCompanyId
    FROM appraisal.AppraisalAssignments aa
    WHERE aa.AppraisalId = t.AppraisalId
    ORDER BY aa.CreatedAt DESC
) asg
WHERE t.rnLatest = 1
  -- Real estate only. A machinery-only or property-less appraisal is not reportable.
  AND pm.HasRealEstate = 1
  -- Block projects wait for the AS400 unit key; see the header.
  AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = t.AppraisalId)
  -- "No news" is not "released": only what AS400 has explicitly reported as redeemed is dropped.
  AND ISNULL(h.IsRedeemed, 0) = 0
  -- An appraisal that several later appraisals continue from is already reported through them; see
  -- SupersededBranch, which also holds the guard for the case where none of them reach the file.
  AND NOT EXISTS (SELECT 1 FROM SupersededBranch sb WHERE sb.AppraisalId = t.AppraisalId)

UNION ALL

-- ═══════════════════════════ SOURCE 2 — AS400 legacy listing ═══════════════════════════
-- Reported on the strength of the source alone. Location, land area, building age, usable area and
-- building type go out blank: the listing has no such data and there is no other source. Accepted by
-- the business — this matches what v1 emitted for these rows.
SELECT
    lg.AppraisalNumber                                           AS LatestAppraisalNumber,
    'UNK'                                                        AS CollateralType,
    lg.HostCollateralId,
    NULL                                                         AS LatestAppraisalType,
    CAST(0 AS bit)                                               AS IsUnderConstruction,
    CAST(100 AS decimal(5, 2))                                   AS ConstructionProgressPercent,
    lg.ValuationPriceInBaht                                      AS LatestAppraisalValue,
    lg.ValuationPriceInBaht                                      AS EarliestAppraisalValue,
    NULL                                                         AS CurrentValue,
    NULL                                                         AS SellingPrice,
    NULL                                                         AS NumberOfFloors,
    NULL                                                         AS BuildingAge,
    CAST(lg.ValuationDate AS datetime2)                          AS LatestAppraisalDate,
    NULL                                                         AS LatestProgressiveAppraisalDate,
    CAST(lg.ValuationDate AS datetime2)                          AS EarliestAppraisalDate,
    NULL                                                         AS LatestAppraisalCompanyId,
    NULL                                                         AS DopaCode,
    NULL                                                         AS LandAreaSqWa,
    NULL                                                         AS BuildingArea,
    NULL                                                         AS BuildingTypeCode,
    NULL                                                         AS BuildingTypeDescription
FROM Legacy lg
LEFT JOIN collateral.HostCollateralLinks h2 ON h2.AppraisalNumber = lg.AppraisalNumber
WHERE
    -- Still reported by AS400 and not released.
    ISNULL(h2.IsRedeemed, 0) = 0
    AND EXISTS (SELECT 1 FROM collateral.HostCollateralLinks h3
                WHERE h3.HostCollateralId = lg.HostCollateralId)
    -- A real appraisal chain has not taken this collateral over.
    AND NOT EXISTS (SELECT 1 FROM LiveHostIds lh WHERE lh.HostCollateralId = lg.HostCollateralId);
