-- ⚠ V2 — PARKED HERE ON PURPOSE (Scripts/Maintenance is embedded but never executed by DbUp).
-- Identical to Views/Reporting/vw_MisCasReport.sql except the Land CTE also filters
-- PricingAnalysisMethods.Role, a column added by migration
-- 20260919160843_AddRoleAndLinkedMethodIdToPricingAnalysisMethods, which is NOT on main / production yet.
-- A view that references a missing column fails `migrate` (SQL 207) and blocks the whole deploy, so move
-- this file to Database/Scripts/Views/Reporting/ only in the same release as that migration.
--
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
CREATE OR ALTER VIEW reporting.vw_MisCasReport_V2
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
Land AS (
    SELECT pg.AppraisalId, SUM(pfv.LandValue) AS LandValue
    FROM appraisal.PricingFinalValues pfv
    JOIN appraisal.PricingAnalysisMethods pam ON pam.Id = pfv.PricingMethodId AND pam.IsSelected = 1
        -- Multi-select Cost: only the land-bearing method's LandValue (same filter as LandValueSql).
        AND (pam.Role IS NULL OR pam.Role IN ('Land', 'LandAndBuilding'))
    JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId AND paa.IsSelected = 1
    JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId AND pa.SubjectType = 0
    JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
    GROUP BY pg.AppraisalId
),

-- Buildings with no inspection are finished, so they count at full depreciated value.
CompletedBuilding AS (
    SELECT ap.AppraisalId, SUM(bdd.PriceAfterDepreciation) AS CompletedBuildingValue
    FROM appraisal.BuildingDepreciationDetails bdd
    JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
    JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
    WHERE NOT EXISTS (SELECT 1 FROM appraisal.ConstructionInspections ci
                      WHERE ci.AppraisalPropertyId = ap.Id)
    GROUP BY ap.AppraisalId
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

-- One set of construction figures per appraisal, as the service returns them. When no inspection
-- has a value base of its own (a condo unit), the appraised value stands in as the 100% / previous /
-- current figure and land + completed buildings drop out — the appraised value already contains them.
Construction AS (
    SELECT
        ci.AppraisalId,
        HasOwnValueBase = CAST(CASE WHEN ci.TotalValue > 0 THEN 1 ELSE 0 END AS bit),
        LandValue       = CASE WHEN ci.TotalValue > 0 THEN ISNULL(l.LandValue, 0) ELSE 0 END,
        CompletedValue  = CASE WHEN ci.TotalValue > 0 THEN ISNULL(cb.CompletedBuildingValue, 0) ELSE 0 END,
        InspTotal       = CASE WHEN ci.TotalValue > 0 THEN ci.TotalValue    ELSE va.AppraisedValue END,
        InspPrevious    = CASE WHEN ci.TotalValue > 0 THEN ci.PreviousValue ELSE va.AppraisedValue END,
        InspCurrent     = CASE WHEN ci.TotalValue > 0 THEN ci.CurrentValue  ELSE va.AppraisedValue END,
        PreviousPct     = CASE WHEN ci.TotalValue > 0 THEN ci.WeightedPreviousPct ELSE ci.UnweightedPreviousPct END,
        CurrentPct      = CASE WHEN ci.TotalValue > 0 THEN ci.WeightedCurrentPct  ELSE ci.UnweightedCurrentPct END
    FROM Ci ci
    LEFT JOIN Land l ON l.AppraisalId = ci.AppraisalId
    LEFT JOIN CompletedBuilding cb ON cb.AppraisalId = ci.AppraisalId
    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = ci.AppraisalId
    -- The service reports nothing for an inspection with neither a value base nor an appraised value.
    WHERE ci.TotalValue > 0 OR ISNULL(va.AppraisedValue, 0) <> 0
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
         THEN con.LandValue + con.CompletedValue + con.InspCurrent
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
    con.LandValue + con.CompletedValue + con.InspTotal          AS SumLandBuildingAmt,   -- 86
    con.LandValue + con.CompletedValue + con.InspCurrent        AS CurrLandBuildingAmt,  -- 87
    con.LandValue + con.CompletedValue + con.InspPrevious       AS OldLandBuildingAmt,   -- 88
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
