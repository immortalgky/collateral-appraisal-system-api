-- MIS CAS Report — the legacy CAS "Application" table rebuilt from the new schema, for MIS.
-- Spec: "MIS CAS Report.xlsx" — the blue fields of sheet "Application" plus sheet "เพิ่มเติม".
-- Column names keep the legacy names so MIS can map them one-to-one; the number in each comment is
-- the row number ("No.") in the spec.
--
-- GRAIN: one row per appraisal. Block / project appraisals (those with an appraisal.Projects row)
-- are excluded — they appraise a whole development, not one customer's collateral.
--
-- BASE TABLES ONLY, on purpose: this view must not move when another view is reworked. The logic
-- it shares is copied, not referenced, from:
--   * latest assignment ........ collateral.vw_CollateralResultExport (LatestAssignment)
--   * construction progress .... IConstructionCurrentValueService.CiAggregateSql / LandValueSql /
--                                CompletedBuildingValueSql (also mirrored in vw_RegulatoryExport)
--   * origination walk ......... collateral.vw_RegulatoryExport (Walk / Earliest)
--   * survey date fallback ..... appraisal.vw_AppraisalDetail (AppraisalDate)
-- If one of those rules changes, change it here too.
--
-- Legacy fields the new system does not hold are emitted as NULL so the column list stays complete:
-- CustomerTitleId, ContactTitleId (the title is part of the name), CurrForcedSaleAmt,
-- CurrForcedSalePC, ApprovedNote, IsPrintBoard. RefCPCOLID is dropped ("not use" in the spec).
--
-- OriginationValue walks PrevAppraisalId recursively. A chain deeper than 100 needs the caller to
-- add OPTION (MAXRECURSION 0) — it cannot live inside a view.
CREATE OR ALTER VIEW reporting.vw_MisCasReport
AS
WITH
Base AS (
    SELECT a.Id AS AppraisalId, a.RequestId, a.PrevAppraisalId
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = a.Id)
),

-- Same pick as vw_CollateralResultExport: the newest assignment that was not rejected or cancelled.
LatestAssignment AS (
    SELECT Id, AppraisalId, AssigneeCompanyId
    FROM (
        SELECT asg.Id, asg.AppraisalId, asg.AssigneeCompanyId,
               ROW_NUMBER() OVER (PARTITION BY asg.AppraisalId
                                  ORDER BY asg.AssignedAt DESC, asg.CreatedAt DESC, asg.Id DESC) AS rn
        FROM appraisal.AppraisalAssignments asg
        WHERE asg.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) z
    WHERE rn = 1
),

-- Route-back rounds. Appraisal workflow tasks carry the RequestId as CorrelationId.
RouteBack AS (
    SELECT ct.CorrelationId AS RequestId, COUNT(*) AS RoundNo
    FROM workflow.CompletedTasks ct
    WHERE ct.Movement = 'B'
    GROUP BY ct.CorrelationId
),

-- ── Construction progress (copy of ConstructionCurrentValueService) ───────────────────────────
-- Land: only the SELECTED approach and method count, one value per group.
--
-- As of 2026-09-24 PricingFinalValues.LandValue is written by the COST approach only: market,
-- income and residual price a collateral as one lump, so ApplyLandAreaValue clears the column for
-- them rather than storing the whole property's value under a name that says land. There is no
-- approach filter here because a non-cost row saved SINCE that change contributes NULL by itself.
-- Rows last saved before it still hold their market lump, so this SUM is a mix until each is saved
-- again; no backfill shipped, by decision. If that becomes a problem, filter on the approach type
-- rather than waiting for the data to catch up.
--
-- Consequence, the same one ConstructionCurrentValueService carries and for the same reason: an
-- appraisal priced by the market approach contributes no land to columns 82 and 86-88, so
-- SumLandBuildingAmt reports its buildings alone. Previously it reported the market lump PLUS the
-- buildings, counting them twice. Neither is right; this formula needs a separable land figure and
-- the market approach cannot produce one. Left as-is because these columns describe construction
-- jobs, which are priced with the cost approach in practice.
-- The groups these construction columns are about: those holding an inspected property. An
-- appraisal that also carries machinery, a bare plot or a second condo in their own groups was
-- dragging all of it in while the progress percentage came from the inspected buildings alone.
-- Machinery needs no filter of its own — a machinery group has no inspection, so it never enters
-- the set. Both CTEs below fall back to the whole appraisal when no inspected property sits in a
-- group. KEEP IN SYNC with ConstructionCurrentValueService.CiGroupsSql.
CiGroups AS (
    SELECT DISTINCT ap.AppraisalId, gi.PropertyGroupId
    FROM appraisal.ConstructionInspections ci
    JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
    JOIN appraisal.PropertyGroupItems gi ON gi.AppraisalPropertyId = ap.Id
),

Land AS (
    SELECT pg.AppraisalId, SUM(pfv.LandValue) AS LandValue
    FROM appraisal.PricingFinalValues pfv
    JOIN appraisal.PricingAnalysisMethods pam ON pam.Id = pfv.PricingMethodId AND pam.IsSelected = 1
        -- Multi-select Cost: only the land-bearing method's LandValue (same filter as LandValueSql).
        -- Safe on every environment since migration 20260919160843 landed on main (cc246ea4).
        AND (pam.Role IS NULL OR pam.Role IN ('Land', 'LandAndBuilding'))
    JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId AND paa.IsSelected = 1
    JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId AND pa.SubjectType = 0
    JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
    WHERE (EXISTS (SELECT 1 FROM CiGroups cg
                   WHERE cg.AppraisalId = pg.AppraisalId AND cg.PropertyGroupId = pg.Id)
           OR NOT EXISTS (SELECT 1 FROM CiGroups cg WHERE cg.AppraisalId = pg.AppraisalId))
      AND EXISTS (SELECT 1
                  FROM appraisal.ConstructionInspections ciG
                  JOIN appraisal.AppraisalProperties apG ON apG.Id = ciG.AppraisalPropertyId
                  WHERE apG.AppraisalId = pg.AppraisalId)
    GROUP BY pg.AppraisalId
),

-- Buildings with no inspection are finished, so they count at full value: the appraiser's own
-- Building Cost Value where they keyed one, else the depreciated sum rounded to the nearest 1,000.
-- Driven from the building and LEFT JOINed to its schedule, because keying an override INSTEAD of
-- filling in a depreciation table leaves no BuildingDepreciationDetails rows at all.
-- KEEP IN SYNC with ConstructionCurrentValueService.CompletedBuildingValueSql.
CompletedBuilding AS (
    SELECT b.AppraisalId, SUM(b.BuildingValue) AS CompletedBuildingValue
    FROM (
        SELECT ap.AppraisalId,
               ISNULL(COALESCE(bad.FinalCostValueOverride,
                               ROUND(SUM(bdd.PriceAfterDepreciation), -3)), 0) AS BuildingValue
        FROM appraisal.BuildingAppraisalDetails bad
        JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
        LEFT JOIN appraisal.BuildingDepreciationDetails bdd ON bdd.BuildingAppraisalDetailId = bad.Id
        WHERE NOT EXISTS (SELECT 1 FROM appraisal.ConstructionInspections ci
                          WHERE ci.AppraisalPropertyId = ap.Id)
          AND (EXISTS (SELECT 1 FROM appraisal.PropertyGroupItems gi
                       JOIN CiGroups cg ON cg.PropertyGroupId = gi.PropertyGroupId
                                       AND cg.AppraisalId = ap.AppraisalId
                       WHERE gi.AppraisalPropertyId = ap.Id)
               OR NOT EXISTS (SELECT 1 FROM CiGroups cg WHERE cg.AppraisalId = ap.AppraisalId))
          -- Same guard as GroupValues, and for the same reason: this aggregates before the join, so
          -- the optimiser cannot push the appraisal predicate in. Without it every building in the
          -- database is grouped and valued on every read of the view.
          AND EXISTS (SELECT 1
                      FROM appraisal.ConstructionInspections ciG
                      JOIN appraisal.AppraisalProperties apG ON apG.Id = ciG.AppraisalPropertyId
                      WHERE apG.AppraisalId = ap.AppraisalId)
        GROUP BY ap.AppraisalId, bad.Id, bad.FinalCostValueOverride
    ) b
    GROUP BY b.AppraisalId
),

-- Part-built buildings at 100% / previous / current. Summary-mode money is derived from the
-- percentage (SummaryPreviousValue / SummaryCurrentValue are always saved as 0); money is rounded to
-- whole baht per inspection (CA-614); progress is weighted by TotalValue, never money / money.
Ci AS (
    SELECT
        ap.AppraisalId,
        SUM(v.TotalValue)                      AS TotalValue,
        SUM(ROUND(v.PreviousValue, 0))         AS PreviousValue,
        SUM(ROUND(v.CurrentValue, 0))          AS CurrentValue,
        AVG(v.PreviousPct)                     AS UnweightedPreviousPct,
        AVG(v.CurrentPct)                      AS UnweightedCurrentPct,
        CASE WHEN SUM(v.TotalValue) > 0
             THEN SUM(v.TotalValue * v.PreviousPct) / SUM(v.TotalValue) END AS WeightedPreviousPct,
        CASE WHEN SUM(v.TotalValue) > 0
             THEN SUM(v.TotalValue * v.CurrentPct) / SUM(v.TotalValue) END  AS WeightedCurrentPct
    FROM appraisal.ConstructionInspections ci
    JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
    LEFT JOIN (
        SELECT ConstructionInspectionId,
               SUM(PreviousPropertyValue)                       AS PreviousPropertyValueSum,
               SUM(CurrentPropertyValue)                        AS CurrentPropertyValueSum,
               SUM(ProportionPct * PreviousProgressPct / 100.0) AS PreviousProportionPctSum,
               SUM(CurrentProportionPct)                        AS CurrentProportionPctSum
        FROM appraisal.ConstructionWorkDetails
        GROUP BY ConstructionInspectionId
    ) wd ON wd.ConstructionInspectionId = ci.Id
    CROSS APPLY (
        SELECT
            ci.TotalValue,
            CASE WHEN ci.IsFullDetail = 0 THEN ISNULL(ci.SummaryPreviousProgressPct, 0)
                 ELSE ISNULL(wd.PreviousProportionPctSum, 0) END AS PreviousPct,
            CASE WHEN ci.IsFullDetail = 0 THEN ISNULL(ci.SummaryCurrentProgressPct, 0)
                 ELSE ISNULL(wd.CurrentProportionPctSum, 0) END  AS CurrentPct,
            CASE WHEN ci.IsFullDetail = 0
                 THEN ci.TotalValue * ISNULL(ci.SummaryPreviousProgressPct, 0) / 100.0
                 ELSE ISNULL(wd.PreviousPropertyValueSum, 0) END AS PreviousValue,
            CASE WHEN ci.IsFullDetail = 0
                 THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                 ELSE ISNULL(wd.CurrentPropertyValueSum, 0) END  AS CurrentValue
    ) v
    GROUP BY ap.AppraisalId
),

-- The price of record for the inspected groups, on the same three-step rule as
-- ConstructionCurrentValueService.AppraisedValueSql: the inspected groups' own prices, or — only
-- when no inspected property sits in a group — the appraisal-level valuation. An inspected group
-- with no price answers 0 rather than borrowing another group's money.
GroupValues AS (
    SELECT pg.AppraisalId,
           COALESCE(pa.FinalAppraisedValue, grp.EffectiveValue) AS GroupValue,
           CASE WHEN EXISTS (SELECT 1 FROM CiGroups cg
                             WHERE cg.AppraisalId = pg.AppraisalId AND cg.PropertyGroupId = pg.Id)
                THEN 1 ELSE 0 END AS InCiGroup
    FROM appraisal.PropertyGroups pg
    LEFT JOIN appraisal.PricingAnalysis pa ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
    OUTER APPLY (
        SELECT SUM(COALESCE(pm.MethodValue, fv.IndicatedValue, fv.FinalValue)) AS EffectiveValue
        FROM appraisal.PricingAnalysisApproaches pap
        JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
        LEFT JOIN appraisal.PricingFinalValues fv ON fv.PricingMethodId = pm.Id
        WHERE pap.PricingAnalysisId = pa.Id AND pap.IsSelected = 1
    ) grp
    -- Only appraisals that carry an inspection at all — the same population the Ci CTE keeps, and
    -- the only one these columns are about. Without it this walks every property group in the
    -- database and prices each one, which is a whole-schema pricing scan on a MIS extract. The rest
    -- of the view leans on the optimiser pushing the appraisal predicate down; this CTE aggregates
    -- before the join, so it cannot.
    WHERE EXISTS (
        SELECT 1
        FROM appraisal.ConstructionInspections ci
        JOIN appraisal.AppraisalProperties apx ON apx.Id = ci.AppraisalPropertyId
        WHERE apx.AppraisalId = pg.AppraisalId)
),

ScopedAppraised AS (
    SELECT gv.AppraisalId,
           SUM(CASE WHEN gv.InCiGroup = 1 THEN gv.GroupValue END) AS ScopedValue,
           SUM(gv.GroupValue)                                     AS AllGroupsValue,
           MAX(gv.InCiGroup)                                      AS HasCiGroup
    FROM GroupValues gv
    GROUP BY gv.AppraisalId
),

-- One set of construction figures per appraisal, as the service returns them. When no inspection
-- has a value base of its own (a condo unit), the appraised value stands in as the 100% / previous /
-- current figure and land + completed buildings drop out — the appraised value already contains them.
ConstructionBase AS (
    SELECT
        ci.AppraisalId,
        av.AppraisedValue,
        HasOwnValueBase = CAST(CASE WHEN ci.TotalValue > 0 THEN 1 ELSE 0 END AS bit),
        LandValue       = CASE WHEN ci.TotalValue > 0 THEN ISNULL(l.LandValue, 0) ELSE 0 END,
        CompletedValue  = CASE WHEN ci.TotalValue > 0 THEN ISNULL(cb.CompletedBuildingValue, 0) ELSE 0 END,
        -- With no value base of its own (a condo unit has no depreciation table to total) the
        -- SCOPED appraised value stands in — never va.AppraisedValue, which covers the whole
        -- appraisal and would put a machinery group's money on a page about one unit.
        InspTotal       = CASE WHEN ci.TotalValue > 0 THEN ci.TotalValue    ELSE av.AppraisedValue END,
        InspPrevious    = CASE WHEN ci.TotalValue > 0 THEN ci.PreviousValue ELSE av.AppraisedValue END,
        InspCurrent     = CASE WHEN ci.TotalValue > 0 THEN ci.CurrentValue  ELSE av.AppraisedValue END,
        PreviousPct     = CASE WHEN ci.TotalValue > 0 THEN ci.WeightedPreviousPct ELSE ci.UnweightedPreviousPct END,
        CurrentPct      = CASE WHEN ci.TotalValue > 0 THEN ci.WeightedCurrentPct  ELSE ci.UnweightedCurrentPct END
    FROM Ci ci
    LEFT JOIN Land l ON l.AppraisalId = ci.AppraisalId
    LEFT JOIN CompletedBuilding cb ON cb.AppraisalId = ci.AppraisalId
    LEFT JOIN ScopedAppraised sa ON sa.AppraisalId = ci.AppraisalId
    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = ci.AppraisalId
    CROSS APPLY (
        -- valuation ?? rollup on the no-group arm, as AppraisedValueSql and the book both do: an
        -- appraisal priced group by group need not have a committed ValuationAnalyses row yet, and
        -- answering 0 there dropped it out of the WHERE below entirely.
        SELECT AppraisedValue = CASE WHEN ISNULL(sa.HasCiGroup, 0) = 1 THEN ISNULL(sa.ScopedValue, 0)
                                     ELSE COALESCE(va.AppraisedValue, sa.AllGroupsValue, 0) END
    ) av
    -- No WHERE: an inspection with neither a value base nor a price of record still has a recorded
    -- progress, and the percentage columns (89-91) are not money. Dropping the row here blanked them
    -- for a collateral that is genuinely mid-construction. The money columns come out 0, which is
    -- what "not priced yet" looks like. Mirrors GetAsync, which stopped returning null for this.
),

-- The three reported milestones, mirroring ConstructionValueBreakdown: the completed value is the
-- price of record (the components only when the groups carry no price), and the part-built figures
-- are lifted to it at 100% and capped by it below — so the current figure can never print above the
-- finished one, and a re-inspection of finished work cannot report progress money at 0% progress.
-- KEEP IN SYNC with ConstructionValueBreakdown.CompleteValue / CurrentValue / PreviousValue.
Construction AS (
    SELECT c.*,
           CompleteValue = x.CompleteValue,
           CurrentValue  = CASE WHEN c.CurrentPct >= 100 THEN x.CompleteValue
                                WHEN c.LandValue + c.CompletedValue + c.InspCurrent < x.CompleteValue
                                     THEN c.LandValue + c.CompletedValue + c.InspCurrent
                                ELSE x.CompleteValue END,
           PreviousValue = CASE WHEN c.PreviousPct >= 100 THEN x.CompleteValue
                                WHEN c.LandValue + c.CompletedValue + c.InspPrevious < x.CompleteValue
                                     THEN c.LandValue + c.CompletedValue + c.InspPrevious
                                ELSE x.CompleteValue END
    FROM ConstructionBase c
    CROSS APPLY (
        SELECT CompleteValue = CASE WHEN c.AppraisedValue > 0 THEN c.AppraisedValue
                                    ELSE c.LandValue + c.CompletedValue + c.InspTotal END
    ) x
),

-- Government price, summed the way the Decision Summary totals it (GetDecisionSummaryQueryHandler):
-- land titles of grouped properties, skipping titles missing from the survey; plus every condo unit.
GovPrice AS (
    SELECT AppraisalId, SUM(GovernmentPrice) AS GovernmentPrice
    FROM (
        SELECT ap.AppraisalId, lt.GovernmentPrice
        FROM appraisal.LandTitles lt
        JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
        JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
        WHERE ISNULL(lt.IsMissingFromSurvey, 0) = 0
          AND EXISTS (SELECT 1 FROM appraisal.PropertyGroupItems gi WHERE gi.AppraisalPropertyId = ap.Id)
        UNION ALL
        SELECT ap.AppraisalId, cad.GovernmentPrice
        FROM appraisal.CondoAppraisalDetails cad
        JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
    ) g
    GROUP BY AppraisalId
),

-- ── Origination value (copy of vw_RegulatoryExport Walk / Earliest) ───────────────────────────
-- Cycle guard is the Path test, not a depth limit — a depth limit would silently return the Nth
-- ancestor as if it were the first.
Walk AS (
    SELECT b.AppraisalId, b.AppraisalId AS AncestorId, b.PrevAppraisalId, 0 AS Depth,
           CAST('|' + CAST(b.AppraisalId AS varchar(36)) + '|' AS varchar(max)) AS Path
    FROM Base b

    UNION ALL

    SELECT w.AppraisalId, p.Id, p.PrevAppraisalId, w.Depth + 1,
           CAST(w.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
    FROM Walk w
    JOIN appraisal.Appraisals p ON p.Id = w.PrevAppraisalId AND p.IsDeleted = 0
    WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', w.Path) = 0
      -- A block project is not a step in one collateral's history.
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = p.Id)
),

Earliest AS (
    SELECT AppraisalId, AppraisedValue
    FROM (
        SELECT w.AppraisalId, v.AppraisedValue,
               ROW_NUMBER() OVER (PARTITION BY w.AppraisalId
                                  ORDER BY v.ValuationDate ASC, w.Depth DESC) AS rn
        FROM Walk w
        JOIN appraisal.ValuationAnalyses v ON v.AppraisalId = w.AncestorId
    ) z
    WHERE rn = 1
)

SELECT
    b.AppraisalId,
    a.AppraisalNumber,
    r.Id                                                        AS ApplicationId,        -- 1
    rd.PrevAppraisalId                                          AS RefApplicationId,     -- 2
    rd.PrevAppraisalNumber                                      AS OldItemNo,            -- 3
    rd.PrevAppraisalDate                                        AS OldSurveyDate,        -- 4
    r.RequestNumber                                             AS ApplicationNo,        -- 5
    a.Status                                                    AS StatusId,             -- 6
    ISNULL(rb.RoundNo, 0)                                       AS RoundNo,              -- 7
    r.RequestorName                                             AS Informer,             -- 8
    r.Requestor                                                 AS EmCode,               -- 9
    ru.AoCode                                                   AS OfficerCode,          -- 10
    ru.PhoneNumber                                              AS EmPhone,              -- 11
    rof.CostCenterCode                                          AS CostCenterCode,       -- 12
    dep.DivisionCode                                            AS Division,             -- 13
    COALESCE(dep.Description, ru.Department)                    AS Department,           -- 14
    r.Purpose                                                   AS ObjectiveId,          -- 16
    pp.Description                                              AS ObjectiveDesc,
    rd.PrevAppraisalValue                                       AS OldAppraisalAmt,      -- 17
    rd.AdditionalFacilityLimit                                  AS NewCreditFacAmt,      -- 18
    rd.PreviousFacilityLimit                                    AS OldCreditFacAmt,      -- 19
    rd.FacilityLimit                                            AS CreditFacLimit,       -- 20
    CAST(NULL AS int)                                           AS CustomerTitleId,      -- 21
    cust.Name                                                   AS CustomerName,         -- 22
    CAST(NULL AS int)                                           AS ContactTitleId,       -- 23
    rd.ContactPersonName                                        AS ContactName,          -- 24
    cust.ContactNumber                                          AS Phone,                -- 25
    rd.DealerCode                                               AS DealerCode,           -- 26
    dl.DealerName                                               AS DealerDesc,           -- 27
    -- No customer address exists in the new system; the request's property address stands in.
    rd.HouseNumber                                              AS AddrNo,               -- 28
    rd.ProjectName                                              AS AddrBuilding,         -- 29
    rd.Road                                                     AS AddrStreet,           -- 30
    fee.FeeNotes                                                AS PayFeeDetail,         -- 49
    va.ValuationApproach                                        AS ApproachTypeId,       -- 50 (already text: Market / Cost / Combined …)
    COALESCE(va.ValuationDate, appt.AppointmentDateTime, a.CompletedAt) AS SurveyDate,   -- 51
    gp.GovernmentPrice                                          AS AgenturerRate,        -- 52
    CASE WHEN con.AppraisalId IS NOT NULL
         THEN con.CurrentValue
         ELSE va.AppraisedValue END                             AS CurrAppraisalAmt,     -- 53
    CAST(NULL AS decimal(19, 2))                                AS CurrForcedSaleAmt,    -- 54
    va.AppraisedValue                                           AS TotalAppraisalAmt,    -- 55
    va.InsuranceValue                                           AS InsuranceAmt,         -- 56
    va.ForcedSaleValue                                          AS ForcedSaleAmt,        -- 57
    d.Condition                                                 AS Condition,            -- 58
    d.Remark                                                    AS Remark,               -- 59
    d.InternalAppraiserOpinion                                  AS AssessorNote,         -- 60
    d.CommitteeOpinion                                          AS BoardNote,            -- 61
    CAST(NULL AS nvarchar(max))                                 AS ApprovedNote,         -- 62
    a.IsDeleted                                                 AS IsDeleted,            -- 63
    a.CreatedBy                                                 AS IssueBy,              -- 64
    a.CreatedAt                                                 AS IssueDate,            -- 65
    a.UpdatedBy                                                 AS UpdatedBy,            -- 66
    a.UpdatedAt                                                 AS UpdatedDate,          -- 67
    a.CompletedAt                                               AS ApprovedDate,         -- 74
    CAST(NULL AS int)                                           AS IsPrintBoard,         -- 75
    a.AppraisalType                                             AS CategoryId,           -- 76
    -- PayFeeAmt (77) and the "fee per case" of sheet เพิ่มเติม, split before / VAT / after.
    fee.TotalFeeBeforeVAT                                       AS FeeBeforeVat,
    fee.VATAmount                                               AS FeeVatAmount,
    fee.TotalFeeAfterVAT                                        AS FeeAfterVat,
    va.AppraisedValue                                           AS OtherAppraised,       -- 78
    va.ForceSaleRate                                            AS ForcedSalePC,         -- 79
    CAST(NULL AS decimal(9, 2))                                 AS CurrForcedSalePC,     -- 80
    fp.ProjectName                                              AS ConstructionName,     -- 81
    con.LandValue                                               AS SumLandCostAmt,       -- 82
    con.CompletedValue + con.InspTotal                          AS SumBuildingCostAmt,   -- 83
    con.CompletedValue + con.InspCurrent                        AS CurrBuildingCostAmt,  -- 84
    con.CompletedValue + con.InspPrevious                       AS OldBuildingCostAmt,   -- 85
    con.CompleteValue                                           AS SumLandBuildingAmt,   -- 86
    con.CurrentValue                                            AS CurrLandBuildingAmt,  -- 87
    con.PreviousValue                                           AS OldLandBuildingAmt,   -- 88
    CAST(con.PreviousPct AS decimal(9, 2))                      AS JobOldPC,             -- 89
    CAST(con.CurrentPct - con.PreviousPct AS decimal(9, 2))     AS JobIncreasePC,        -- 90
    CAST(con.CurrentPct AS decimal(9, 2))                       AS JobCurrPC,            -- 91
    e.AppraisedValue                                            AS OriginationValue,     -- 100

    -- ── Sheet "เพิ่มเติม" ──
    COALESCE(NULLIF(co.NameLocal, N''), co.Name)                AS AppraisalCompanyName,
    rd.LoanApplicationNumber                                    AS LosApplicationNo,
    CASE WHEN r.IsPma = 1 THEN 'Y' ELSE 'N' END                 AS IsPma,
    -- Purchase price vs appraised value. There is no refinance/redemption amount in the new system,
    -- so refinance cases come out NULL.
    CAST(rd.TotalSellingPrice * 100.0 / NULLIF(va.AppraisedValue, 0) AS decimal(9, 2)) AS PriceToAppraisalPct,
    va.InsuranceValue                                           AS FireInsuranceAmt
FROM Base b
JOIN appraisal.Appraisals a ON a.Id = b.AppraisalId
JOIN request.Requests r ON r.Id = b.RequestId AND r.IsDeleted = 0
LEFT JOIN request.RequestDetails rd ON rd.RequestId = r.Id
LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = b.AppraisalId
LEFT JOIN appraisal.AppraisalDecisions d ON d.AppraisalId = b.AppraisalId
LEFT JOIN LatestAssignment la ON la.AppraisalId = b.AppraisalId
LEFT JOIN auth.Companies co ON co.Id = TRY_CAST(la.AssigneeCompanyId AS uniqueidentifier) AND co.IsDeleted = 0
LEFT JOIN RouteBack rb ON rb.RequestId = r.Id
LEFT JOIN Construction con ON con.AppraisalId = b.AppraisalId
LEFT JOIN Earliest e ON e.AppraisalId = b.AppraisalId
LEFT JOIN GovPrice gp ON gp.AppraisalId = b.AppraisalId
LEFT JOIN auth.AspNetUsers ru ON ru.UserName = r.Requestor
LEFT JOIN auth.Officers rof ON rof.OfficerCode = ru.AoCode
LEFT JOIN auth.Departments dep ON dep.Code = rof.DepartmentCode
LEFT JOIN parameter.Dealers dl ON dl.DealerCode = rd.DealerCode
OUTER APPLY (SELECT TOP 1 f.FeeNotes, f.TotalFeeBeforeVAT, f.VATAmount, f.TotalFeeAfterVAT
             FROM appraisal.AppraisalFees f
             WHERE f.AssignmentId = la.Id
             ORDER BY f.CreatedAt DESC, f.Id DESC) fee
OUTER APPLY (SELECT TOP 1 c.Name, c.ContactNumber
             FROM request.RequestCustomers c
             WHERE c.RequestId = r.Id
             ORDER BY c.Id) cust
OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
             WHERE [Group] = 'AppraisalPurpose' AND [Language] = 'TH' AND Code = r.Purpose) pp
-- Latest non-cancelled appointment on the latest assignment — the SurveyDate fallback.
OUTER APPLY (SELECT TOP 1 apt.AppointmentDateTime
             FROM appraisal.Appointments apt
             WHERE apt.AssignmentId = la.Id AND apt.Status <> 'Cancelled'
             ORDER BY apt.AppointmentDateTime DESC) appt
-- First property (by SequenceNumber) that is land or condo: its village / condo name.
OUTER APPLY (SELECT TOP 1
                    COALESCE(NULLIF(ld.Village, N''), cd.CondoName)    AS ProjectName
             FROM appraisal.AppraisalProperties p
             LEFT JOIN appraisal.LandAppraisalDetails ld ON ld.AppraisalPropertyId = p.Id
             LEFT JOIN appraisal.CondoAppraisalDetails cd ON cd.AppraisalPropertyId = p.Id
             WHERE p.AppraisalId = b.AppraisalId
               AND (ld.Id IS NOT NULL OR cd.Id IS NOT NULL)
             ORDER BY p.SequenceNumber) fp;
