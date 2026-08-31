-- Outbound AS400 "Collateral Result" — one row per result still owed to the host.
--
-- ── Why this replaces the old query ────────────────────────────────────────────────────────────
-- The previous version started from collateral.CollateralEngagements joined to CollateralMasters and
-- used `CollateralMaster.HostCollateralId IS NOT NULL` as the eligibility gate. That made the file a
-- function of whether a CollateralMaster had been created — and 6,699 completed appraisals on the
-- production-like dataset never get one (a condo with no sub-district, land with no title number, a
-- leasehold that will not resolve). Those appraisals were never sent, and nothing said so: no error,
-- no warning, no row.
--
-- Here the row set is the appraisals themselves. A missing master cannot hide one.
--
-- ── Finding the AS400 collateral id ────────────────────────────────────────────────────────────
-- AS400 stamps our appraisal number into CCSURV when it mints a collateral id, and only moves it
-- when a NEW drawdown happens. A reappraisal involves no drawdown, so the id keeps pointing at the
-- older appraisal: 91.3% of held master-title rows name the head of their chain rather than the
-- latest round. Joining on the appraisal's own number would therefore find nothing for exactly the
-- reappraisals this file exists to report — and where the head had never been sent, would ship a
-- price from years ago.
--
-- So each completed appraisal walks UP PrevAppraisalId and stops at the first ancestor AS400 knows.
-- Nearer ancestor wins: it reflects the more recent drawdown.
--
-- The walk finds the ID only. Every value in the record — price, dates, valuer, areas — comes from
-- the appraisal that was actually completed.
--
-- ── One row, or none ───────────────────────────────────────────────────────────────────────────
-- An appraisal is sent with Auto Update 'Y' only when the walk lands on exactly one collateral. Land
-- more than one and we cannot say which collateral the price belongs to, so the id is left blank and
-- the flag goes out as 'N' for a human at the bank to resolve. A blank id still identifies the work:
-- the appraisal number is in the record.
--
-- Block projects are the exception, because there the ambiguity is resolvable. AS400 mints an id per
-- financed unit and names that unit twice — in CollateralName as "CONDO.<key> <deeds>", and as the
-- leading word of Address1. Either key can find the unit in appraisal.ProjectUnits, and the match
-- gives one row per financed collateral, each with its own price and its own land area rather than
-- the development's total repeated.
--
-- ── OPTION (MAXRECURSION 0) ────────────────────────────────────────────────────────────────────
-- Cannot live inside a view; the caller supplies it. The walk is guarded by the visited path rather
-- than a depth cap: construction-inspection chains run to dozens of rounds (34 measured), and a
-- `Depth < 20` style guard does not error when it bites — it silently returns the 20th ancestor as
-- the root, which here would mean quietly attaching the wrong collateral id.
CREATE OR ALTER VIEW collateral.vw_CollateralResultExport
AS
WITH

-- ── What AS400 currently holds ─────────────────────────────────────────────────────────────────
-- COLLATLINK is a full monthly replace: rows the newest file omits are collateral the bank no longer
-- holds. They stay in the table (a truncated file has to be recoverable) so the active filter lives
-- in every reader, here included.
Active AS (
    SELECT
        k.HostCollateralId,
        k.CollateralName,
        k.CasAppraisalNumber,
        k.NameToken,
        k.AddrToken
    FROM collateral.vw_HostCollateralLinkKeys k
    WHERE k.IsRedeemed = 0
      AND k.IsActive = 1
),

-- ── The appraisals still owed ──────────────────────────────────────────────────────────────────
-- Completed and not yet acknowledged for the id they would carry. The ledger is keyed on
-- (AppraisalId, CollateralId), so an appraisal sent earlier with a blank id is still owed once AS400
-- mints one — it pairs with a different key and goes out again.
Pending AS (
    SELECT a.Id AS AppraisalId, a.AppraisalNumber, a.PrevAppraisalId
    FROM appraisal.Appraisals a
    WHERE a.Status = 'Completed'
      AND a.IsDeleted = 0
      -- Narrowed before the walk, not after. The chain walk is recursive and the sent-ledger filter
      -- sits at the very end, so without this every completed appraisal the system has ever produced
      -- would be walked on every run to discard almost all of it. Three cases can still owe a row:
      AND (
          -- never sent at all
          NOT EXISTS (SELECT 1 FROM collateral.CollateralResultLogs l WHERE l.AppraisalId = a.Id)
          -- sent before AS400 had minted an id, so it is owed another turn once one exists
          OR EXISTS (SELECT 1 FROM collateral.CollateralResultLogs l
                      WHERE l.AppraisalId = a.Id AND l.CollateralId = '')
          -- a block project sends one row per unit and can gain units between runs, so "already sent"
          -- never settles it
          OR EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = a.Id)
      )
),

Walk AS (
    SELECT
        p.AppraisalId,
        p.AppraisalId AS AncestorId,
        p.PrevAppraisalId,
        0 AS Depth,
        CAST('|' + CAST(p.AppraisalId AS varchar(36)) + '|' AS varchar(max)) AS Path
    FROM Pending p

    UNION ALL

    SELECT w.AppraisalId, anc.Id, anc.PrevAppraisalId, w.Depth + 1,
           CAST(w.Path + CAST(anc.Id AS varchar(36)) + '|' AS varchar(max))
    FROM Walk w
    JOIN appraisal.Appraisals anc ON anc.Id = w.PrevAppraisalId AND anc.IsDeleted = 0
    WHERE CHARINDEX('|' + CAST(anc.Id AS varchar(36)) + '|', w.Path) = 0
),

-- Every collateral reachable from an appraisal, with how far up it was found.
Hit AS (
    SELECT
        w.AppraisalId,
        w.Depth,
        act.HostCollateralId,
        act.NameToken,
        act.AddrToken
    FROM Walk w
    JOIN appraisal.Appraisals anc ON anc.Id = w.AncestorId
    JOIN Active act ON act.CasAppraisalNumber = anc.AppraisalNumber
),

-- Only the nearest ancestor that matched. A newer drawdown supersedes an older one, and mixing the
-- two levels would count the same collateral twice and make every such appraisal look ambiguous.
NearestHit AS (
    SELECT h.AppraisalId, h.HostCollateralId, h.NameToken, h.AddrToken
    FROM Hit h
    WHERE h.Depth = (SELECT MIN(h2.Depth) FROM Hit h2 WHERE h2.AppraisalId = h.AppraisalId)
),

HitCount AS (
    SELECT AppraisalId, COUNT(*) AS Matches
    FROM NearestHit
    GROUP BY AppraisalId
),

-- ── Values, all from the completed appraisal ───────────────────────────────────────────────────
Val AS (
    SELECT v.AppraisalId, v.ValuationDate, v.AppraisedValue, v.ForcedSaleValue
    FROM appraisal.ValuationAnalyses v
),

-- Which property types the appraisal holds, which decides where age and area are read from.
PropMix AS (
    SELECT
        p.AppraisalId,
        MAX(CASE WHEN p.PropertyType IN ('B','LB','LSB','LS') THEN 1 ELSE 0 END) AS HasBuilding,
        MAX(CASE WHEN p.PropertyType IN ('U','LSU')           THEN 1 ELSE 0 END) AS HasCondo,
        MAX(CASE WHEN p.PropertyType = 'MAC'                  THEN 1 ELSE 0 END) AS HasMachine
    FROM appraisal.AppraisalProperties p
    GROUP BY p.AppraisalId
),

-- Land area for the whole appraisal, in square wa. Rai/Ngan/SquareWa are stored separately on the
-- title, so this is a conversion INTO the total rather than out of it.
LandTotal AS (
    SELECT
        p.AppraisalId,
        SUM(ISNULL(t.AreaRai, 0) * 400 + ISNULL(t.AreaNgan, 0) * 100 + ISNULL(t.AreaSquareWa, 0)) AS TotalSqWa
    FROM appraisal.AppraisalProperties p
    JOIN appraisal.LandAppraisalDetails d ON d.AppraisalPropertyId = p.Id
    JOIN appraisal.LandTitles t           ON t.LandAppraisalDetailId = d.Id
    WHERE p.PropertyType IN ('L','LB','LSL','LS')
    GROUP BY p.AppraisalId
),

-- The appraisal's own Rai/Ngan/Wa, summed and then normalised. Summing the three columns straight
-- would emit values like "2 rai 7 ngan 430 wa"; carrying up keeps each part inside its own field.
LandParts AS (
    SELECT
        AppraisalId,
        -- FLOOR, not plain division: TotalSqWa is decimal (AreaSquareWa carries fractions), and in
        -- T-SQL decimal / int is decimal division. 1145.50 / 400 is 2.86, not 2 rai, and the same
        -- mistake in the ngan term would put 3.45 into a dec(3,0) field.
        CAST(FLOOR(TotalSqWa / 400) AS int)            AS Rai,
        CAST(FLOOR((TotalSqWa % 400) / 100) AS int)    AS Ngan,
        TotalSqWa % 100                                AS SquareWa
    FROM LandTotal
),

-- Oldest building and total footprint. A single building cannot speak for a plot holding several,
-- and the host uses the oldest structure to drive depreciation. Identical to the rule in
-- vw_RegulatoryExportV3 — the two must be changed together.
BuildingAgg AS (
    SELECT
        p.AppraisalId,
        MAX(b.BuildingAge)       AS MaxBuildingAge,
        SUM(b.TotalBuildingArea) AS TotalBuildingArea
    FROM appraisal.AppraisalProperties p
    JOIN appraisal.BuildingAppraisalDetails b ON b.AppraisalPropertyId = p.Id
    WHERE p.PropertyType IN ('B','LB','LSB','LS')
    GROUP BY p.AppraisalId
),

CondoAgg AS (
    SELECT AppraisalId, UsableArea, BuildingAge
    FROM (
        SELECT p.AppraisalId, c.UsableArea, c.BuildingAge,
               ROW_NUMBER() OVER (PARTITION BY p.AppraisalId ORDER BY p.SequenceNumber) AS rn
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.CondoAppraisalDetails c ON c.AppraisalPropertyId = p.Id
        WHERE p.PropertyType IN ('U','LSU')
    ) z
    WHERE rn = 1
),

MachineLife AS (
    SELECT AppraisalId, LifeSpanYears
    FROM (
        SELECT p.AppraisalId, mci.LifeSpanYears,
               ROW_NUMBER() OVER (PARTITION BY p.AppraisalId ORDER BY p.SequenceNumber) AS rn
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.MachineCostItems mci ON mci.AppraisalPropertyId = p.Id
        WHERE p.PropertyType = 'MAC' AND mci.LifeSpanYears IS NOT NULL
    ) z
    WHERE rn = 1
),

-- ── Land and building components ───────────────────────────────────────────────────────────────
-- Pricing is held per property GROUP, not per appraisal: the selected approach's selected method
-- carries the final values. Land and building components only exist on a cost approach that priced
-- a building; every other approach reports the total alone, which is why both come back NULL there.
SelectedPricing AS (
    SELECT AppraisalId, PropertyGroupId, UnitPrice, BuildingValue
    FROM (
        SELECT
            gi.AppraisalId,
            gi.PropertyGroupId,
            CASE WHEN ap.ApproachType = 'Cost' AND fv.HasBuildingValue = 1
                 THEN fv.FinalValueAdjusted END AS UnitPrice,
            CASE WHEN ap.ApproachType = 'Cost' AND fv.HasBuildingValue = 1
                 THEN fv.BuildingValue END      AS BuildingValue,
            ROW_NUMBER() OVER (
                PARTITION BY gi.AppraisalId
                -- Prefer the selected cost approach, then any selected approach, mirroring
                -- GetAppraisalForCollateralQueryHandler so the two cannot disagree.
                ORDER BY CASE WHEN ap.IsSelected = 1 AND ap.ApproachType = 'Cost' THEN 0
                              WHEN ap.IsSelected = 1 THEN 1
                              ELSE 2 END,
                         CASE WHEN m.IsSelected = 1 THEN 0 ELSE 1 END,
                         gi.SequenceNumber) AS rn
        FROM (
            SELECT DISTINCT p.AppraisalId, pgi.PropertyGroupId, p.SequenceNumber
            FROM appraisal.AppraisalProperties p
            JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = p.Id
        ) gi
        JOIN appraisal.PricingAnalysis pa        ON pa.AnchorId = gi.PropertyGroupId
        JOIN appraisal.PricingAnalysisApproaches ap ON ap.PricingAnalysisId = pa.Id
        JOIN appraisal.PricingAnalysisMethods m  ON m.ApproachId = ap.Id
        JOIN appraisal.PricingFinalValues fv     ON fv.PricingMethodId = m.Id
    ) z
    WHERE rn = 1
),

-- The land the selected pricing actually priced.
--
-- LandValue is a rate multiplied by an area, and the two have to describe the same land. UnitPrice
-- comes from ONE property group's cost approach, so the area must be that group's as well. Using the
-- appraisal-wide total instead reported 84,090,837 of land on appraisal 69000180 — whose whole
-- appraised value is 61,726,000 — because the appraisal holds a second group, priced by a market
-- approach, whose land was swept into the multiplication at the first group's rate.
--
-- LandAreaRai/Ngan/Wa and LandAreaTotalSqWa are a different question and still come from LandTotal:
-- the spec asks those to describe the whole appraisal, not the priced group.
SelectedGroupLand AS (
    SELECT
        sp.AppraisalId,
        SUM(ISNULL(t.AreaRai, 0) * 400 + ISNULL(t.AreaNgan, 0) * 100 + ISNULL(t.AreaSquareWa, 0)) AS GroupSqWa
    FROM SelectedPricing sp
    JOIN appraisal.PropertyGroupItems pgi ON pgi.PropertyGroupId = sp.PropertyGroupId
    JOIN appraisal.AppraisalProperties p  ON p.Id = pgi.AppraisalPropertyId
                                         AND p.AppraisalId = sp.AppraisalId
    JOIN appraisal.LandAppraisalDetails d ON d.AppraisalPropertyId = p.Id
    JOIN appraisal.LandTitles t           ON t.LandAppraisalDetailId = d.Id
    WHERE p.PropertyType IN ('L','LB','LSL','LS')
    GROUP BY sp.AppraisalId
),

-- ── Who did the work ───────────────────────────────────────────────────────────────────────────
-- The latest assignment that was not rejected or cancelled. A case routed back from a company to an
-- internal appraiser leaves a company-less latest assignment, and that is the answer: nobody
-- external holds it now.
LatestAssignment AS (
    SELECT AppraisalId, AssigneeCompanyId, InternalAppraiserName, AppraiserUserId
    FROM (
        SELECT
            asg.AppraisalId,
            asg.AssigneeCompanyId,
            asg.InternalAppraiserName,
            COALESCE(asg.AssigneeUserId, asg.InternalAppraiserId, asg.ExternalAppraiserId) AS AppraiserUserId,
            ROW_NUMBER() OVER (PARTITION BY asg.AppraisalId
                               ORDER BY asg.AssignedAt DESC, asg.CreatedAt DESC, asg.Id DESC) AS rn
        FROM appraisal.AppraisalAssignments asg
        WHERE asg.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) z
    WHERE rn = 1
),

Valuer AS (
    SELECT
        la.AppraisalId,
        la.AssigneeCompanyId,
        -- AssigneeCompanyId is nvarchar while auth.Companies.Id is uniqueidentifier; a non-Guid value
        -- becomes NULL and the lookup simply misses.
        (SELECT TOP 1 c.Name FROM auth.Companies c
          WHERE c.Id = TRY_CAST(la.AssigneeCompanyId AS uniqueidentifier) AND c.IsDeleted = 0) AS CompanyName,
        (SELECT TOP 1 c.HostCompanyCode FROM auth.Companies c
          WHERE c.Id = TRY_CAST(la.AssigneeCompanyId AS uniqueidentifier) AND c.IsDeleted = 0) AS CompanyCode,
        COALESCE(
            (SELECT TOP 1 NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, ' ', u.LastName))), '')
               FROM auth.AspNetUsers u WHERE u.UserName = la.AppraiserUserId),
            la.InternalAppraiserName) AS InternalAppraiserName,
        -- Users are keyed by UserName (the bank code), not by Id.
        (SELECT TOP 1 u.EmployeeId FROM auth.AspNetUsers u
          WHERE u.UserName = la.AppraiserUserId) AS EmployeeId
    FROM LatestAssignment la
),

-- ── Block projects: one row per financed collateral ────────────────────────────────────────────
-- Every unit key a collateral names, from either field, one row per key. AS400 can name several
-- rooms in one field as a comma list, and one collateral covering three rooms is one collateral —
-- the parts are summed back together below, not sent as three rows under the same id.
UnitToken AS (
    SELECT
        nh.AppraisalId,
        nh.HostCollateralId,
        -- 0 = the name, 1 = the address. The name outranks the address; see UnitSource.
        s.Source,
        LTRIM(RTRIM(pv.value)) AS Part
    FROM NearestHit nh
    CROSS APPLY (VALUES (0, nh.NameToken), (1, nh.AddrToken)) AS s(Source, Token)
    CROSS APPLY STRING_SPLIT(ISNULL(s.Token, ''), ',') pv
    -- The empty-string guard is load-bearing: a blank part matches every unit whose column is blank
    -- and would price a collateral from an unrelated room.
    WHERE LTRIM(RTRIM(pv.value)) <> ''
),

-- The unit rows those keys name. appraisal.vw_ProjectUnitKeys is what knows which column of the unit
-- table can carry the key and how the three rank.
UnitHit AS (
    SELECT
        t.AppraisalId,
        t.HostCollateralId,
        t.Source,
        t.Part,
        k.ProjectUnitId                     AS UnitId,
        MIN(up.TotalAppraisalValueRounded)  AS UnitValue,
        MIN(k.LandArea)                     AS UnitLandAreaSqWa,
        MIN(k.KeyRank)                      AS KeyRank
    FROM UnitToken t
    JOIN appraisal.Projects pr        ON pr.AppraisalId = t.AppraisalId
    JOIN appraisal.vw_ProjectUnitKeys k ON k.ProjectId = pr.Id AND k.UnitKey = t.Part
    -- Unpriced units still match. Dropping them would blank an id we can prove, and the row would go
    -- out as 'N' for a human to resolve when we already know exactly which collateral it is.
    LEFT JOIN appraisal.ProjectUnitPrices up ON up.ProjectUnitId = k.ProjectUnitId
    GROUP BY t.AppraisalId, t.HostCollateralId, t.Source, t.Part, k.ProjectUnitId
),

-- ONE VALUE PER ROOM, not per unit row. The unit table holds the same room more than once — room
-- 630/32 of one project exists as two rows, both priced 3,760,000 — and summing rows reported
-- 7,520,000 for a single-room collateral. Summing one value per room the key actually names is what
-- "the sum of the rooms" means, and it survives however many times CAS stored each room.
UnitPerRoom AS (
    SELECT AppraisalId, HostCollateralId, Source, Part, UnitId, UnitValue, UnitLandAreaSqWa
    FROM (
        SELECT h.*,
               ROW_NUMBER() OVER (
                   PARTITION BY h.AppraisalId, h.HostCollateralId, h.Source, h.Part
                   ORDER BY h.KeyRank,
                            CASE WHEN ISNULL(h.UnitValue, 0) > 0 THEN 0 ELSE 1 END,
                            h.UnitId) AS rn
        FROM UnitHit h
    ) z
    WHERE rn = 1
),

-- One source per collateral, the name preferred. Mixing them would add the address match and the
-- name match of the same collateral together and report it twice over.
UnitSource AS (
    SELECT AppraisalId, HostCollateralId, MIN(Source) AS Source
    FROM UnitPerRoom
    GROUP BY AppraisalId, HostCollateralId
),

ProjectUnitMatch AS (
    SELECT
        r.AppraisalId,
        r.HostCollateralId,
        MIN(r.UnitId)                        AS ProjectUnitId,
        SUM(ISNULL(r.UnitValue, 0))          AS UnitValue,
        SUM(ISNULL(r.UnitLandAreaSqWa, 0))   AS UnitLandAreaSqWa
    FROM UnitPerRoom r
    JOIN UnitSource s ON s.AppraisalId     = r.AppraisalId
                     AND s.HostCollateralId = r.HostCollateralId
                     AND s.Source           = r.Source
    GROUP BY r.AppraisalId, r.HostCollateralId
),

-- An appraisal is a block project when it HAS a Projects row — not when its units happened to match.
-- Deriving this from ProjectUnitMatch meant a project whose unit keys did not line up fell through to
-- the ordinary path, and if AS400 happened to report a single collateral for it that id was sent as
-- though it described the whole development. It describes one unit.
IsBlock AS (
    SELECT DISTINCT pr.AppraisalId
    FROM appraisal.Projects pr
),

-- A block project is a PreAppraisal of the whole development: it carries no AppraisalProperties, so
-- LandTotal finds nothing for it and the whole-appraisal area would go out as zero. Its area is the
-- sum of its units.
ProjectLandTotal AS (
    SELECT pr.AppraisalId, SUM(u.LandArea) AS TotalSqWa
    FROM appraisal.Projects pr
    JOIN appraisal.ProjectUnits u ON u.ProjectId = pr.Id
    GROUP BY pr.AppraisalId
),

-- Assembled first so the sent-ledger filter below can see the collateral id each row resolved to;
-- that id is part of the key and cannot be known before this point.
Assembled AS (
SELECT
    p.AppraisalId,
    p.AppraisalNumber                                        AS AppraisalReportNumber,

    -- Blank whenever the collateral could not be pinned down to one. The appraisal number still
    -- identifies the work, and Auto Update tells the host to look at it by hand.
    CASE
        WHEN pum.HostCollateralId IS NOT NULL THEN pum.HostCollateralId
        WHEN blk.AppraisalId IS NULL AND hc.Matches = 1 THEN nh.HostCollateralId
        ELSE ''
    END                                                      AS CollateralId,

    CASE
        WHEN pum.HostCollateralId IS NOT NULL THEN 'Y'
        WHEN blk.AppraisalId IS NULL AND hc.Matches = 1 THEN 'Y'
        ELSE 'N'
    END                                                      AS AutoUpdate,

    -- A unit reports its own appraised value. When it has none the row goes out as zero rather than
    -- falling back to the appraisal total: that total is the whole development, and quoting it
    -- against one unit would overstate that collateral by orders of magnitude.
    CASE WHEN pum.HostCollateralId IS NOT NULL THEN ISNULL(pum.UnitValue, 0)
         ELSE v.AppraisedValue END                           AS AppraisalValue,
    -- Land component: the cost approach's adjusted unit price over the land THAT approach priced.
    -- There is no stored column for it — the same multiplication the engagement froze at completion,
    -- which also scoped both halves to one property group. See SelectedGroupLand.
    CASE WHEN sp.UnitPrice IS NOT NULL AND sgl.GroupSqWa IS NOT NULL
         THEN sp.UnitPrice * sgl.GroupSqWa END               AS LandValue,
    sp.BuildingValue                                         AS BuildingValue,
    v.ForcedSaleValue                                        AS ForceSaleValue,

    CAST(v.ValuationDate AS date)                            AS CurrentAppraisalDate,
    DATEADD(YEAR, 3, CAST(v.ValuationDate AS date))          AS NextAppraisalDate,

    -- An appraisal ran on the external path or the internal path, never both, so exactly one pair is
    -- populated and the other is blank. The test is "a company is attached", the same rule the rest
    -- of the system uses.
    -- The path decision itself, not left to be inferred. An external appraisal whose company row is
    -- missing or whose AssigneeCompanyId is not a Guid resolves to no name AND no code, and a caller
    -- guessing from those two nulls would classify it as internal and emit the bank staffer's details
    -- for work a company did.
    CASE WHEN vl.AssigneeCompanyId IS NOT NULL THEN 1 ELSE 0 END             AS IsExternal,
    CASE WHEN vl.AssigneeCompanyId IS NULL THEN vl.EmployeeId END            AS InternalValuerEmployeeId,
    CASE WHEN vl.AssigneeCompanyId IS NULL THEN vl.InternalAppraiserName END AS InternalValuerName,
    CASE WHEN vl.AssigneeCompanyId IS NOT NULL THEN vl.CompanyCode END       AS ExternalValuerCode,
    CASE WHEN vl.AssigneeCompanyId IS NOT NULL THEN vl.CompanyName END       AS ExternalValuerName,

    ml.LifeSpanYears                                         AS LifeYear,

    -- Age and area by what the appraisal actually holds. Bare land and machinery report neither: the
    -- field means usable floor area, and land area is carried in its own fields below in sq.wa.
    CASE WHEN pm.HasBuilding = 1 THEN ba.MaxBuildingAge
         WHEN pm.HasCondo = 1    THEN ca.BuildingAge END     AS BuildingAge,
    CASE WHEN pm.HasBuilding = 1 THEN ba.TotalBuildingArea
         WHEN pm.HasCondo = 1    THEN ca.UsableArea END      AS AreaUtilization,

    -- Per-row land area: the unit's own for a block, the appraisal's otherwise.
    CASE WHEN pum.HostCollateralId IS NOT NULL
         THEN CAST(FLOOR(pum.UnitLandAreaSqWa / 400) AS int)
         ELSE lp.Rai END                                     AS LandAreaRai,
    CASE WHEN pum.HostCollateralId IS NOT NULL
         THEN CAST(FLOOR((pum.UnitLandAreaSqWa % 400) / 100) AS int)
         ELSE lp.Ngan END                                    AS LandAreaNgan,
    CASE WHEN pum.HostCollateralId IS NOT NULL
         THEN pum.UnitLandAreaSqWa % 100
         ELSE lp.SquareWa END                                AS LandAreaSquareWa,

    -- Whole-appraisal total, identical on every row of the same appraisal by design: it is the sum,
    -- not this row's share.
    COALESCE(plt.TotalSqWa, lt.TotalSqWa)                    AS LandAreaTotalSqWa

FROM Pending p
LEFT JOIN HitCount hc          ON hc.AppraisalId = p.AppraisalId
LEFT JOIN IsBlock blk          ON blk.AppraisalId = p.AppraisalId
-- Only joined for the unambiguous non-block case; a block takes its id from the unit match instead.
LEFT JOIN NearestHit nh        ON nh.AppraisalId = p.AppraisalId
                              AND blk.AppraisalId IS NULL
                              AND hc.Matches = 1
LEFT JOIN ProjectUnitMatch pum ON pum.AppraisalId = p.AppraisalId
LEFT JOIN Val v                ON v.AppraisalId = p.AppraisalId
LEFT JOIN PropMix pm           ON pm.AppraisalId = p.AppraisalId
LEFT JOIN LandTotal lt         ON lt.AppraisalId = p.AppraisalId
LEFT JOIN ProjectLandTotal plt ON plt.AppraisalId = p.AppraisalId
LEFT JOIN LandParts lp         ON lp.AppraisalId = p.AppraisalId
LEFT JOIN BuildingAgg ba       ON ba.AppraisalId = p.AppraisalId
LEFT JOIN CondoAgg ca          ON ca.AppraisalId = p.AppraisalId
LEFT JOIN MachineLife ml       ON ml.AppraisalId = p.AppraisalId
LEFT JOIN SelectedPricing sp   ON sp.AppraisalId = p.AppraisalId
LEFT JOIN SelectedGroupLand sgl ON sgl.AppraisalId = p.AppraisalId
LEFT JOIN Valuer vl            ON vl.AppraisalId = p.AppraisalId
)

SELECT r.*
FROM Assembled r
-- Not yet acknowledged for this exact (appraisal, collateral) pair. Keying on the pair rather than
-- the appraisal alone is what lets a block send one row per unit, and what gives an appraisal sent
-- with a blank id another turn once AS400 mints one.
WHERE NOT EXISTS (
    SELECT 1
    FROM collateral.CollateralResultLogs l
    WHERE l.AppraisalId = r.AppraisalId
      AND l.CollateralId = r.CollateralId
);
