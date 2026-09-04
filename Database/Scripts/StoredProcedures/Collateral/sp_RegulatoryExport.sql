-- The regulatory (Basel/RDT) snapshot: one row per collateral AS400 says the bank holds, carrying
-- that collateral's first appraisal. Read by RegulatoryExportQuery, written out by
-- RegulatoryFileWriter (fixed-width, for AS400) and RegulatoryExcelWriter (the Risk team's copy).
--
-- ── Why this is a procedure and not a view ────────────────────────────────────────────────────
-- It was a view, and the view stopped finishing. Read with every column, as the export reads it, it
-- ran past the job's 600-second command timeout and no file was produced at all. COUNT(*) still
-- returned in under a second, which is how it went unnoticed: a count lets the optimiser prune the
-- CTE branches it does not need.
--
-- A CTE is not a temporary table. Every reference re-expands it, and this query referenced six of
-- them more than once:
--
--     Src 3x   Anchor 3x   Walk 3x (recursive)   Val 3x   ProjectWalk 3x (recursive)   Legacy 2x
--
-- Nested, that multiplies: the join to appraisal.Appraisals ran many times over and inside two
-- recursions, and the row estimate reached 20 billion. On an estimate like that the optimiser stops
-- hashing and starts replaying a full scan of every appraisal per link row.
--
-- Materialising each shared step fixes both halves at once. Each runs exactly once, and a #temp
-- carries real statistics, so every step after it is planned against the row count it will actually
-- see rather than a guess.
--
-- ── What this actually bought, measured ───────────────────────────────────────────────────────
-- NOT wall-clock. On a small set with the right indexes in place the view is the faster of the two,
-- because its plan goes parallel while the #temp steps run one after another. What changes is that
-- the cost stops depending on a guess. Between U3 and UAT3 — the same report, 1.9% more rows:
--
--                     U3          UAT3
--     view   reads    2,860,562   3,153,276    +10%
--            CPU ms       6,853      19,643   +187%   <- 1.9% more data
--     proc   reads      896,493   1,184,672    +32%
--            CPU ms       9,732      10,321     +6%
--
-- The view's estimate reaches 20 billion rows, so the plan it gets is whatever the optimiser
-- happens to guess; when the guess is wrong the cost does not degrade gracefully, and production
-- ran past ten minutes on a set 467 rows larger than UAT3's. Every #temp step here is planned
-- against a row count that is already known, which is why the procedure's cost barely moves.
--
-- ⚠ This has NOT been shown to fix production. The reason production overran is still unexplained,
-- and the argument above is a shape, not a measurement of that box. Run the procedure there and
-- compare before believing it.
--
-- The same lesson, from the same codebase: QuickSearchQueryHandler already notes that "a CTE
-- referenced twice is re-evaluated, which would run all 17 arms again".
--
-- ── Reading this against the old view ─────────────────────────────────────────────────────────
-- The logic is unchanged and was verified row-for-row against it (24,418 rows, EXCEPT empty both
-- directions). Each block below is the CTE of the same name, lifted verbatim; only the references
-- between them changed, from CTE name to #temp.
--
-- OPTION (MAXRECURSION 0) now lives with the two recursive steps that need it. In the view it could
-- not, so it had to be appended by the caller and applied to the whole statement.
CREATE OR ALTER PROCEDURE [collateral].[sp_RegulatoryExport]
AS
BEGIN
    SET NOCOUNT ON;

    WITH Src AS (
        SELECT
            k.HostCollateralId,
            k.AppraisalNumber,
            k.CasAppraisalNumber,
            k.TicketAppraisalId,
            k.AddrToken,
            k.NameToken,
            k.TicketToken,
            k.PropertyType,
            k.PropertyTypeDesc
        FROM collateral.vw_HostCollateralLinkKeys k
        -- Both 'Y' and 'N' are reported; only a row where the feed never stated the flag is left out.
        -- The bank's own file carries collateral flagged 'N' (collateral 59305 appears three times in the
        -- 2026-08-02 file), so treating 'N' as unreportable dropped 114 collateral the bank does report.
        -- A blank is different: the row stopped short of pos 172 and AS400 said nothing at all.
        WHERE k.MasterTitle IS NOT NULL
          AND k.IsRedeemed = 0
          AND k.IsActive = 1
    )
    SELECT * INTO #Src FROM Src;
    CREATE UNIQUE CLUSTERED INDEX ix ON #Src(HostCollateralId);

    WITH Anchor AS (
        -- Collateral AS400 named by appraisal number, which is all of them from before ticketing.
        SELECT s.HostCollateralId, s.PropertyType, s.PropertyTypeDesc,
               a.Id AS AppraisalId, a.AppraisalNumber, a.AppraisalType, a.RequestId, a.PrevAppraisalId,
               pr.ProjectType
        FROM #Src s
        JOIN appraisal.Appraisals a
            ON  a.AppraisalNumber = s.CasAppraisalNumber
            AND a.IsDeleted       = 0
        LEFT JOIN appraisal.Projects pr ON pr.AppraisalId = a.Id
        WHERE s.TicketAppraisalId IS NULL

        UNION ALL

        -- Collateral AS400 named by a ticket we issued. The id is already in hand, so this is a seek.
        SELECT s.HostCollateralId, s.PropertyType, s.PropertyTypeDesc,
               a.Id AS AppraisalId, a.AppraisalNumber, a.AppraisalType, a.RequestId, a.PrevAppraisalId,
               pr.ProjectType
        FROM #Src s
        JOIN appraisal.Appraisals a
            ON  a.Id        = s.TicketAppraisalId
            AND a.IsDeleted = 0
        LEFT JOIN appraisal.Projects pr ON pr.AppraisalId = a.Id
        WHERE s.TicketAppraisalId IS NOT NULL
    )
    SELECT * INTO #Anchor FROM Anchor;
    CREATE CLUSTERED INDEX ix ON #Anchor(AppraisalId);
    CREATE INDEX ix2 ON #Anchor(HostCollateralId);

    WITH Walk AS (
        SELECT
            an.AppraisalId,
            an.AppraisalId      AS AncestorId,
            an.PrevAppraisalId,
            0                   AS Depth,
            CAST('|' + CAST(an.AppraisalId AS varchar(36)) + '|' AS varchar(max)) AS Path
        FROM #Anchor an

        UNION ALL

        SELECT
            w.AppraisalId,
            p.Id,
            p.PrevAppraisalId,
            w.Depth + 1,
            CAST(w.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
        FROM Walk w
        JOIN appraisal.Appraisals p
            ON  p.Id        = w.PrevAppraisalId
            AND p.IsDeleted = 0
        WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', w.Path) = 0
          -- A block project is not a step in one collateral's history: many unrelated units point at the
          -- same project, and a project can point back at a unit it absorbed. Dead end both ways.
          AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = p.Id)
          AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = w.AncestorId)
    )
    SELECT * INTO #Walk FROM Walk
    OPTION (MAXRECURSION 0);
    CREATE CLUSTERED INDEX ix ON #Walk(AncestorId);
    CREATE INDEX ix2 ON #Walk(AppraisalId, Depth);

    -- No filter. ProjectUnitSum joins Val on an ancestor that comes from ProjectWalk, not Walk, so any
    -- restriction built from #Walk/#Anchor silently drops block-project valuations - it cost two rows
    -- the wrong EarliestAppraisalValue when tried. The table is ~59k rows; copying it whole is cheaper
    -- than being clever.
    SELECT v.AppraisalId, v.ValuationDate, v.AppraisedValue
    INTO #Val
    FROM appraisal.ValuationAnalyses v;
    CREATE CLUSTERED INDEX ix ON #Val(AppraisalId);

    WITH ProjectWalk AS (
        SELECT
            an.AppraisalId,
            an.HostCollateralId,
            NULLIF(s.AddrToken, '') AS AddrToken,
            NULLIF(s.NameToken, '') AS NameToken,
            NULLIF(s.TicketToken, '') AS TicketToken,
            an.AppraisalId AS AncestorId,
            an.PrevAppraisalId,
            0              AS Depth,
            CAST('|' + CAST(an.AppraisalId AS varchar(36)) + '|' AS varchar(max)) AS Path
        FROM #Anchor an
        JOIN #Src s ON s.HostCollateralId = an.HostCollateralId
        WHERE NULLIF(s.AddrToken, '') IS NOT NULL
           OR NULLIF(s.NameToken, '') IS NOT NULL
           OR NULLIF(s.TicketToken, '') IS NOT NULL

        UNION ALL

        -- Unlike the main Walk this DOES step through project appraisals. There it is a dead end because
        -- unrelated units point at a shared project and merging them would confuse two collateral; here
        -- the unit token pins one specific unit, so following the project's own history is exactly right.
        SELECT w.AppraisalId, w.HostCollateralId, w.AddrToken, w.NameToken, w.TicketToken, p.Id, p.PrevAppraisalId,
               w.Depth + 1,
               CAST(w.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
        FROM ProjectWalk w
        JOIN appraisal.Appraisals p ON p.Id = w.PrevAppraisalId AND p.IsDeleted = 0
        WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', w.Path) = 0
    )
    SELECT * INTO #PW FROM ProjectWalk
    OPTION (MAXRECURSION 0);
    CREATE CLUSTERED INDEX ix ON #PW(HostCollateralId);
    CREATE INDEX ix2 ON #PW(AncestorId);

    WITH TokenPart AS (
        SELECT
            w.HostCollateralId,
            w.AncestorId,
            w.Depth,
            -- -1 = the ticket CAS issued, 0 = the name, 1 = the address. See TokenRank below; lower wins.
            s.Source,
            LTRIM(RTRIM(p.value)) AS Part
        FROM #PW w
        CROSS APPLY (VALUES (-1, w.TicketToken), (0, w.NameToken), (1, w.AddrToken)) AS s(Source, Token)
        CROSS APPLY STRING_SPLIT(ISNULL(s.Token, ''), ',') p
        -- The empty-string guard is load-bearing: a blank part would match every unit whose column is
        -- blank and price a collateral from an unrelated room.
        WHERE LTRIM(RTRIM(p.value)) <> ''
    ),
    ProjectUnitHit AS (
        SELECT
            t.HostCollateralId,
            t.AncestorId,
            t.Depth,
            t.Source,
            t.Part,
            k.ProjectUnitId                     AS UnitId,
            MIN(pup.TotalAppraisalValueRounded) AS UnitValue,
            -- Registration first, then the room, then the house number. appraisal.vw_ProjectUnitKeys
            -- carries that order; taking the MIN picks the strongest column that answered to this key.
            MIN(k.KeyRank)                      AS KeyRank
        FROM TokenPart t
        JOIN appraisal.Projects pr          ON pr.AppraisalId = t.AncestorId
        JOIN appraisal.vw_ProjectUnitKeys k ON k.ProjectId = pr.Id AND k.UnitKey = t.Part
        -- Rank 3 is the plot number, and only a ticket may match on it — see vw_ProjectUnitKeys for why
        -- a short plot number cannot be trusted against a token parsed out of free text.
        AND (k.KeyRank < 3 OR t.Source = -1)
        JOIN appraisal.ProjectUnitPrices pup ON pup.ProjectUnitId = k.ProjectUnitId
        WHERE ISNULL(pup.TotalAppraisalValueRounded, 0) > 0
        GROUP BY t.HostCollateralId, t.AncestorId, t.Depth, t.Source, t.Part, k.ProjectUnitId
    ),
    ProjectUnitPerRoom AS (
        SELECT HostCollateralId, AncestorId, Depth, Source, Part, UnitValue, KeyRank
        FROM (
            SELECT h.*,
                   ROW_NUMBER() OVER (PARTITION BY h.HostCollateralId, h.AncestorId, h.Depth,
                                                   h.Source, h.Part
                                      ORDER BY h.KeyRank, h.UnitId) AS rn
            FROM ProjectUnitHit h
        ) z
        WHERE rn = 1
    ),
    ProjectUnitSum AS (
        SELECT
            h.HostCollateralId,
            h.Depth,
            h.Source          AS TokenRank,
            MIN(h.KeyRank)    AS KeyRank,
            SUM(h.UnitValue)  AS UnitValue,
            v.ValuationDate
        FROM ProjectUnitPerRoom h
        LEFT JOIN #Val v ON v.AppraisalId = h.AncestorId
        GROUP BY h.HostCollateralId, h.AncestorId, h.Depth, h.Source, v.ValuationDate
    ),
    ProjectUnitValue AS (
        SELECT
            HostCollateralId,
            MIN(CASE WHEN rnEarliest = 1 THEN UnitValue END) AS EarliestUnitValue,
            MIN(CASE WHEN rnLatest   = 1 THEN UnitValue END) AS LatestUnitValue
        FROM (
            SELECT h.*,
                   -- Oldest survey of this unit = its origination.
                   ROW_NUMBER() OVER (PARTITION BY h.HostCollateralId
                                      ORDER BY h.ValuationDate ASC, h.Depth DESC,
                                               h.TokenRank, h.KeyRank) AS rnEarliest,
                   -- Nearest to the appraisal AS400 named = its current value.
                   ROW_NUMBER() OVER (PARTITION BY h.HostCollateralId
                                      ORDER BY h.Depth ASC, h.ValuationDate DESC,
                                               h.TokenRank, h.KeyRank) AS rnLatest
            FROM ProjectUnitSum h
        ) z
        GROUP BY HostCollateralId
    )
    SELECT * INTO #PUV FROM ProjectUnitValue;
    CREATE UNIQUE CLUSTERED INDEX ix ON #PUV(HostCollateralId);

    WITH ProjectUnitRow AS (
        SELECT DISTINCT w.HostCollateralId
        FROM #PW w
        WHERE EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = w.AncestorId)
    )
    SELECT * INTO #PUR FROM ProjectUnitRow;
    CREATE UNIQUE CLUSTERED INDEX ix ON #PUR(HostCollateralId);

    WITH LegacyByCollateral AS (
        SELECT
            CAST(CAST(l.CollateralID AS bigint) AS varchar(19)) AS HostCollateralId,
            MIN(l.ValuationDate)                                AS ValuationDate,
            MIN(l.ValuationPriceInBaht)                         AS ValuationPriceInBaht
        FROM appraisal.AS400ReportListing l
        WHERE l.CollateralID IS NOT NULL
        GROUP BY CAST(CAST(l.CollateralID AS bigint) AS varchar(19))
    )
    SELECT * INTO #Legacy FROM LegacyByCollateral;
    CREATE UNIQUE CLUSTERED INDEX ix ON #Legacy(HostCollateralId);

    WITH Earliest AS (
        SELECT AppraisalId, AncestorId, ValuationDate, AppraisedValue
        FROM (
            SELECT w.AppraisalId, w.AncestorId, v.ValuationDate, v.AppraisedValue,
                   ROW_NUMBER() OVER (PARTITION BY w.AppraisalId
                                      ORDER BY v.ValuationDate ASC, w.Depth DESC) AS rn
            FROM #Walk w
            JOIN #Val v ON v.AppraisalId = w.AncestorId
        ) z
        WHERE rn = 1
    ),
    Ci AS (
        SELECT
            ap.AppraisalId,
            ISNULL(SUM(v.TotalValue), 0)              AS TotalValue,
            ISNULL(SUM(ROUND(v.CurrentValue, 0)), 0)  AS CurrentValue,
            -- Plain average, all there is to report when there is no value to weight by: a condo unit
            -- has no building depreciation table for the CI screen to total, so its TotalValue is 0.
            ISNULL(AVG(v.CurrentPct), 0)              AS EnteredCurrentPercent,
            -- Per-building progress weighted across buildings by what each is worth. This is what
            -- decides "finished" and what this file reports — deliberately NOT CurrentValue /
            -- TotalValue: money is rounded to whole baht (CA-614), so the rounded parts no longer sum
            -- to the rounded whole and a finished building came out a baht short of its own 100%
            -- figure. Percentages are decimal(7,4) and nothing rounds them; TotalValue is only a
            -- weight here. Mirrors ConstructionValueBreakdown.WeightedCurrentPercent.
            CASE WHEN SUM(v.TotalValue) > 0
                 THEN SUM(v.TotalValue * v.CurrentPct) / SUM(v.TotalValue)
                 ELSE 0 END                           AS WeightedCurrentPercent
        FROM appraisal.ConstructionInspections ci
        JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
        LEFT JOIN (
            SELECT ConstructionInspectionId,
                   SUM(CurrentPropertyValue) AS CurrentSum,
                   SUM(CurrentProportionPct) AS CurrentPctSum
            FROM appraisal.ConstructionWorkDetails
            GROUP BY ConstructionInspectionId
        ) wd ON wd.ConstructionInspectionId = ci.Id
        -- One row per inspection, read per the mode flag: a summary inspection carries its own
        -- percentage, a full-detail one is the sum of its work rows.
        CROSS APPLY (
            SELECT
                ci.TotalValue,
                CASE WHEN ci.IsFullDetail = 0 THEN ISNULL(ci.SummaryCurrentProgressPct, 0)
                     ELSE ISNULL(wd.CurrentPctSum, 0) END  AS CurrentPct,
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.CurrentSum, 0) END     AS CurrentValue
        ) v
        GROUP BY ap.AppraisalId
    ),
    CiEffective AS (
        SELECT
            c.AppraisalId,
            CASE WHEN c.TotalValue > 0 THEN 1 ELSE 0 END AS HasOwnValueBase,
            c.CurrentValue,
            c.EnteredCurrentPercent,
            c.WeightedCurrentPercent
        FROM Ci c
    ),
    PropMix AS (
        SELECT
            p.AppraisalId,
            MAX(CASE WHEN p.PropertyType IN ('L', 'LB')  THEN 1 ELSE 0 END) AS HasLand,
            -- 'LB' is a single property that IS land-and-building: it carries both a
            -- LandAppraisalDetails and a BuildingAppraisalDetails row. Treating a separate 'B' row as the
            -- only building signal typed 34,077 land-and-building records as bare land.
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
    BuildingAgg AS (
        SELECT
            p.AppraisalId,
            MAX(b.BuildingAge)        AS MaxBuildingAge,
            MAX(b.NumberOfFloors)     AS MaxNumberOfFloors,
            SUM(b.TotalBuildingArea)  AS TotalBuildingArea
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.BuildingAppraisalDetails b ON b.AppraisalPropertyId = p.Id
        WHERE p.PropertyType IN ('B', 'LB', 'LSB', 'LS')
        GROUP BY p.AppraisalId
    ),
    CondoAgg AS (
        SELECT
            p.AppraisalId,
            SUM(c.UsableArea) AS UsableArea,
            MAX(c.BuildingAge) AS BuildingAge
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.CondoAppraisalDetails c ON c.AppraisalPropertyId = p.Id
        WHERE p.PropertyType IN ('U', 'LSU')
        GROUP BY p.AppraisalId
    ),
    PropSource AS (
        SELECT AppraisalId, AncestorId AS PropSourceAppraisalId
        FROM (
            SELECT w.AppraisalId, w.AncestorId,
                   ROW_NUMBER() OVER (PARTITION BY w.AppraisalId ORDER BY w.Depth ASC) AS rn
            FROM #Walk w
            WHERE EXISTS (SELECT 1 FROM appraisal.AppraisalProperties p
                          WHERE p.AppraisalId = w.AncestorId)
        ) z
        WHERE rn = 1
    )

    -- ═══════════════════════════ SOURCE 1 — collateral CAS has appraised ═══════════════════════════
    SELECT
        an.AppraisalNumber                                           AS LatestAppraisalNumber,

        -- Derived from the property mix of whichever appraisal actually described the property.
        CASE
            WHEN pm.HasLeaseBoth     = 1 THEN 'LS'
            WHEN pm.HasLeaseBuilding = 1 THEN 'LSB'
            -- Leased land with a building on it is LS, not LSL: the writer sends 'L' — "no structure" —
            -- for L and LSL alike, so an LSL row carrying buildings would tell the regulator the plot is
            -- empty.
            WHEN pm.HasLeaseLand     = 1 AND pm.HasBuilding = 1 THEN 'LS'
            WHEN pm.HasLeaseLand     = 1 THEN 'LSL'
            WHEN pm.HasLeaseCondo    = 1 THEN 'LSU'
            WHEN pm.HasLand = 1 AND pm.HasBuilding = 1 THEN 'LB'
            WHEN pm.HasLand = 1 THEN 'L'
            WHEN pm.HasCondo = 1 THEN 'U'
            WHEN pm.HasBuilding = 1 THEN 'LB'
            -- Nothing in CAS describes the property, which means a block-project appraisal: it is a
            -- PreAppraisal of the whole development and carries no AppraisalProperties. Falling through to
            -- 'L' reported all 1,607 project rows as bare land — 1,552 of them condo units — and the writer
            -- turned that into field 5 = 'L', "no structure". The bank's own file sends 'N' for every one
            -- of the 696 it holds, never 'L'.
            --
            -- ProjectType is CAS's own answer and agrees with what AS400 says in the feed on all 1,607
            -- rows (U↔PCO, LB↔PSH/PTH/PWH), so no code mapping is needed and the undocumented AS400 codes
            -- POT/PHR never have to be guessed at.
            WHEN an.ProjectType IN ('U', 'LB') THEN an.ProjectType
            ELSE 'L'
        END                                                          AS CollateralType,

        an.HostCollateralId,
        an.AppraisalType                                             AS LatestAppraisalType,

        -- Decided on the entered progress, never on the money. See WeightedCurrentPercent above.
        CASE WHEN ci.HasOwnValueBase = 1 AND ci.WeightedCurrentPercent < 100 THEN CAST(1 AS bit)
             -- Valueless inspection (condo): nothing to weight by, so read the plain average of what
             -- was entered rather than reporting a unit that is demonstrably mid-construction as
             -- finished.
             WHEN ci.AppraisalId IS NOT NULL AND ci.HasOwnValueBase = 0
                  AND ISNULL(ci.EnteredCurrentPercent, 0) < 100 THEN CAST(1 AS bit)
             ELSE CAST(0 AS bit) END                                 AS IsUnderConstruction,

        -- Not real estate → 0; bare land → 0; anything with a structure → 100 when complete, else the
        -- progress percentage.
        CASE
            WHEN pm.HasLand = 1 AND pm.HasBuilding = 0 AND pm.HasCondo = 0
                 AND pm.HasLeaseBuilding = 0 AND pm.HasLeaseBoth = 0                     THEN 0
            WHEN pm.HasLeaseLand = 1 AND pm.HasLeaseBuilding = 0 AND pm.HasLeaseBoth = 0
                 AND pm.HasBuilding = 0                                                  THEN 0
            -- No inspection at all → finished. An inspection with no value base reports what was
            -- entered; only a genuinely absent inspection still means 100.
            WHEN ci.AppraisalId IS NULL                                                  THEN 100
            -- Clamped, matching ConstructionValueBreakdown.ConstructionProgressPercent. Nothing
            -- validates that ProportionPct sums to 100, and an unclamped value >= 1000 would overflow
            -- decimal(5,2) and take the whole view down.
            WHEN ci.HasOwnValueBase = 0
                 THEN CAST(
                     CASE WHEN ci.EnteredCurrentPercent < 0   THEN 0
                          WHEN ci.EnteredCurrentPercent > 100 THEN 100
                          ELSE ci.EnteredCurrentPercent END AS decimal(5, 2))
            ELSE CAST(
                     CASE WHEN ci.WeightedCurrentPercent < 0   THEN 0
                          WHEN ci.WeightedCurrentPercent > 100 THEN 100
                          ELSE ci.WeightedCurrentPercent END AS decimal(5, 2))
        END                                                          AS ConstructionProgressPercent,

        -- A project unit's own appraised value beats the project appraisal's 0. Everything else keeps
        -- reading from the appraisal.
        CASE WHEN puv.LatestUnitValue IS NOT NULL         THEN puv.LatestUnitValue
             WHEN pur.HostCollateralId IS NOT NULL       THEN 0
             ELSE av.AppraisedValue END                              AS LatestAppraisalValue,

        -- The origination value, from whichever source holds the older record. AS400 valued a lot of this
        -- collateral before CAS existed, and those valuations live in appraisal.AS400ReportListing under a
        -- '99…' number that is NOT in appraisal.Appraisals — so the PrevAppraisalId walk cannot reach them
        -- however far back it goes. Without this test, 122 collateral report the first CAS appraisal as
        -- their origination: 67A00393 would say 2024-01-26 / 5,750,000 when the bank first valued it on
        -- 2008-11-27 at 4,670,000.
        --
        -- The date and the value must come from the SAME record. Taking MIN of each independently would
        -- pair one source's date with the other's price.
        -- Same for the origination value. A unit has one appraised figure, not a history — the project
        -- was appraised once and the unit inherits that date — so the same number stands for both ends.
        CASE WHEN puv.EarliestUnitValue IS NOT NULL THEN puv.EarliestUnitValue
             -- A project unit with no unit data reports 0; see ProjectUnitRow.
             WHEN pur.HostCollateralId IS NOT NULL THEN 0
             WHEN lgc.ValuationDate IS NOT NULL AND lgc.ValuationDate < e.ValuationDate
                  THEN lgc.ValuationPriceInBaht
             ELSE e.AppraisedValue END                               AS EarliestAppraisalValue,

        -- Still the money figure, but emitted only while the work is unfinished — gated on the same
        -- progress test as IsUnderConstruction so the two can never contradict each other on one row.
        CASE WHEN ci.HasOwnValueBase = 1 AND ci.WeightedCurrentPercent < 100 THEN ci.CurrentValue
             ELSE NULL END                                           AS CurrentValue,

        rd.TotalSellingPrice                                         AS SellingPrice,

        CASE WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1
             THEN CAST(ba.MaxNumberOfFloors AS int) ELSE NULL END    AS NumberOfFloors,

        CASE
            WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1 THEN ba.MaxBuildingAge
            WHEN pm.HasCondo = 1 OR pm.HasLeaseCondo = 1                              THEN ca.BuildingAge
            ELSE NULL
        END                                                          AS BuildingAge,

        av.ValuationDate                                             AS LatestAppraisalDate,


        -- Only meaningful when the appraisal AS400 named is itself a construction round. The chain-based
        -- design searched a chain for the newest Progressive; here the anchor IS the appraisal AS400 considers current, so
        -- looking past it would report a date the bank never asked about.
        CASE WHEN an.AppraisalType = 'Progressive' THEN av.ValuationDate END
                                                                     AS LatestProgressiveAppraisalDate,

        CASE WHEN lgc.ValuationDate IS NOT NULL AND lgc.ValuationDate < e.ValuationDate
             THEN lgc.ValuationDate ELSE e.ValuationDate END      AS EarliestAppraisalDate,

        -- The company the appraisal was assigned to, or NULL when it was done in-house.
        -- RegulatoryFileWriter turns this into field 15: a value means External (1), NULL means
        -- Internal (2).
        --
        -- ⚠ This was a literal NULL from 2026-08-21, when v3 was written from scratch against a new
        -- grain, until it was noticed in the file: every one of the 24,885 rows shipped as Internal.
        -- v1 read it from CollateralEngagement and v2 from the assignment; v3 reached Appraisals
        -- directly and touched neither, so the column lost its source and nobody re-attached it.
        --
        -- The rule restored here is v1's, NOT v2's, and it is copied from
        -- GetAppraisalForCollateralQueryHandler.cs — the C# that fills
        -- CollateralEngagements.AppraisalCompanyId, which is what v1 read. That same column still decides
        -- Internal vs External for the AS400 appraisal-RESULT file (CollateralResultQuery's
        -- IsExternalEngagement), so any other rule here would let the two files the bank receives disagree
        -- about whether one collateral was appraised by an outside firm.
        --
        -- Three things v2's plain ORDER BY CreatedAt DESC did not do:
        --   * skip Rejected and Cancelled — a company that turned the job down never appraised anything
        --   * order by AssignedAt first — it stays NULL until the workflow actually hands the task out, and
        --     DESC sorts NULLs last, so an assignment that was really worked beats one still sitting in
        --     administration
        --   * break ties on Id — without it SQL Server may return either of two rows stamped in the same
        --     transaction, and the field could flip between monthly runs
        --
        -- IF THAT HANDLER'S RULE CHANGES, CHANGE IT HERE AND IN THE OTHER FILE.
        --
        -- TRY_CAST because AssigneeCompanyId is nvarchar(100), not uniqueidentifier.
        asg.AppraisalCompanyId                                       AS LatestAppraisalCompanyId,

        -- DOPA 6-digit sub-district. Administrative address, so it must come from a DOPA-mastered source:
        -- request.RequestDetails, whose picker uses the DOPA master. NOT the deed address, which is
        -- mastered by parameter.TitleSubDistricts and has diverged from DOPA.
        --
        -- A block project carries no AppraisalProperties at all, so PropMix has no row for it and the
        -- HasRealEstate test alone blanked all 1,608 of them. Its request is the developer's, whose
        -- Location IS the project's site, and every unit sits inside that project: on 1,591 of the 1,592
        -- rows this recovers, the code matches the project's own SubDistrict exactly and none disagree.
        -- ProjectType is the same signal CollateralType uses above, so the two fields cover the same rows.
        CASE WHEN pm.HasRealEstate = 1 OR an.ProjectType IN ('U', 'LB')
             THEN (SELECT dsd.Code FROM parameter.DopaSubDistricts dsd
                   WHERE dsd.Code = LTRIM(RTRIM(rd.SubDistrict)))
             ELSE NULL END                                           AS DopaCode,

        -- Two-sided range guards. A value outside [0, 99999.99] overflows the fixed-width field, and a
        -- NEGATIVE one is as fatal as an oversized one because the writer multiplies by 100 and the minus
        -- sign consumes a character. One bad row used to abort the entire file.
        CASE WHEN la.LandAreaSqWa NOT BETWEEN 0 AND 99999.99 THEN NULL
             ELSE la.LandAreaSqWa END                                AS LandAreaSqWa,

        CASE
            WHEN (pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1)
                 AND ba.TotalBuildingArea BETWEEN 0 AND 99999.99 THEN ba.TotalBuildingArea
            WHEN (pm.HasCondo = 1 OR pm.HasLeaseCondo = 1)
                 AND ca.UsableArea BETWEEN 0 AND 99999.99        THEN ca.UsableArea
            ELSE NULL
        END                                                          AS BuildingArea,

        -- AS400's own taxonomy, straight from the feed, description included. CAS's BuildingTypeCode is
        -- deliberately not used: it is "99 อื่นๆ" for 85-100% of rows in every bucket and has no mapping
        -- onto these codes.
        an.PropertyType                                              AS BuildingTypeCode,
        an.PropertyTypeDesc                                          AS BuildingTypeDescription

    FROM #Anchor an
    LEFT JOIN #Val av           ON av.AppraisalId = an.AppraisalId
    LEFT JOIN Earliest e       ON e.AppraisalId  = an.AppraisalId
    LEFT JOIN CiEffective ci   ON ci.AppraisalId = an.AppraisalId
    LEFT JOIN PropSource ps    ON ps.AppraisalId = an.AppraisalId
    LEFT JOIN PropMix pm       ON pm.AppraisalId = ps.PropSourceAppraisalId
    LEFT JOIN LandAgg la       ON la.AppraisalId = ps.PropSourceAppraisalId
    LEFT JOIN BuildingAgg ba   ON ba.AppraisalId = ps.PropSourceAppraisalId
    LEFT JOIN CondoAgg ca      ON ca.AppraisalId = ps.PropSourceAppraisalId
    LEFT JOIN request.RequestDetails rd ON rd.RequestId = an.RequestId
    -- Same rule as GetAppraisalForCollateralQueryHandler.cs — see the note on the column above.
    OUTER APPLY (
        SELECT TOP 1 TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier) AS AppraisalCompanyId
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AppraisalId = an.AppraisalId
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
        ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC, aa.Id DESC
    ) asg
    -- The legacy listing for THIS collateral, if AS400 valued it before CAS existed. Joined on the
    -- collateral id, not the appraisal number: the '99…' number that owns the listing row is a
    -- different number from the one the feed reports today.
    LEFT JOIN #Legacy lgc ON lgc.HostCollateralId = an.HostCollateralId
    LEFT JOIN #PUV puv     ON puv.HostCollateralId = an.HostCollateralId
    LEFT JOIN #PUR pur       ON pur.HostCollateralId = an.HostCollateralId
    -- A collateral with no valuation anywhere in its history has no origination value to report, which is
    -- the one thing this file exists to carry.
    WHERE e.ValuationDate IS NOT NULL

    UNION ALL

    -- ═══════════════════════════ SOURCE 2 — the legacy "99" series ═══════════════════════════
    -- Reported straight from the bank's own migrated data. Everything CAS would normally derive from an
    -- appraisal is absent by definition — there is no appraisal — so only the fields the listing actually
    -- carries are populated and the rest stay NULL, exactly as the retired designs emitted them here.
    SELECT
        s.CasAppraisalNumber                                         AS LatestAppraisalNumber,
        'UNK'                                                        AS CollateralType,
        s.HostCollateralId,
        NULL                                                         AS LatestAppraisalType,
        CAST(0 AS bit)                                               AS IsUnderConstruction,
        -- The bank's own file sends N + 100.00% for this whole series.
        CAST(100 AS decimal(5, 2))                                   AS ConstructionProgressPercent,
        lg.ValuationPriceInBaht                                      AS LatestAppraisalValue,
        -- The listing IS the origination: this collateral has no earlier record anywhere.
        lg.ValuationPriceInBaht                                      AS EarliestAppraisalValue,
        NULL                                                         AS CurrentValue,
        NULL                                                         AS SellingPrice,
        NULL                                                         AS NumberOfFloors,
        NULL                                                         AS BuildingAge,
        lg.ValuationDate                                             AS LatestAppraisalDate,
        NULL                                                         AS LatestProgressiveAppraisalDate,
        lg.ValuationDate                                             AS EarliestAppraisalDate,
        NULL                                                         AS LatestAppraisalCompanyId,
        NULL                                                         AS DopaCode,
        NULL                                                         AS LandAreaSqWa,
        NULL                                                         AS BuildingArea,
        -- AS400 supplies the type for these too, so the series is no longer typeless in the file.
        s.PropertyType                                               AS BuildingTypeCode,
        s.PropertyTypeDesc                                           AS BuildingTypeDescription

    FROM #Src s
    JOIN #Legacy lg ON lg.HostCollateralId = s.HostCollateralId
    -- Only where SOURCE 1 could not report it. A collateral that CAS has appraised is reported from the
    -- appraisal; emitting the listing row as well would double-count one physical collateral.
    WHERE NOT EXISTS (
            SELECT 1 FROM appraisal.Appraisals a
            WHERE a.AppraisalNumber = s.CasAppraisalNumber AND a.IsDeleted = 0)
      -- A ticketed collateral always resolves in SOURCE 1 through TicketAppraisalId. Its
      -- CasAppraisalNumber is the raw ticket string, which matches no appraisal, so without this guard
      -- the test above would pass and the same collateral would be reported from both sources.
      AND s.TicketAppraisalId IS NULL
      AND lg.ValuationDate IS NOT NULL

    -- The grain is the collateral, and one appraisal legitimately covers several of them, so
    -- ordering by appraisal number alone would not be deterministic. This used to sit on the
    -- caller's SELECT; it belongs with the statement it orders.
    ORDER BY HostCollateralId;
END;
