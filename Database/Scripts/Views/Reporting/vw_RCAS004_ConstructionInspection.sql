-- RCAS004 — รายงานการตรวจงวดงานที่ยังไม่ครบ 100 %
-- Appraisals whose progressive construction inspection is below 100%.
-- Per-appraisal progress = MIN across its properties' inspections; per inspection:
--   full-detail  -> SUM(ConstructionWorkDetails.CurrentProportionPct)
--   summary mode -> SummaryCurrentProgressPct
CREATE
OR ALTER VIEW reporting.vw_RCAS004_ConstructionInspection
AS
SELECT v.Id,
       v.CreatedAt              AS AppraisalCreateDate,
       v.AppraisalNumber,
       v.CustomerName,
       v.Purpose,
       v.FacilityLimit          AS ApplyLimitAmount,
       ct.CollateralType,
       v.Channel,
       v.CompanyName            AS AppraisalCompany,
       v.AssigneeUserId         AS InternalAppraisalStaff,
       v.AppraisalValue,
       prev.AppraisalNumber     AS PreviousAppraisalNumber,
       v.AppointmentDateTime    AS AppointmentDate,
       v.Status                 AS AppraisalStatus,
       ci.ProgressPct           AS ProgressiveInspectionPct
FROM appraisal.vw_AppraisalList v
         INNER JOIN (
    SELECT ap.AppraisalId,
           MIN(insp.ProgressPct) AS ProgressPct
    FROM appraisal.ConstructionInspections c
             INNER JOIN appraisal.AppraisalProperties ap ON ap.Id = c.AppraisalPropertyId
             CROSS APPLY (
        SELECT CASE
                   WHEN c.IsFullDetail = 1
                       THEN ISNULL((SELECT SUM(wd.CurrentProportionPct)
                                    FROM appraisal.ConstructionWorkDetails wd
                                    WHERE wd.ConstructionInspectionId = c.Id), 0)
                   ELSE ISNULL(c.SummaryCurrentProgressPct, 0)
               END AS ProgressPct
    ) insp
    GROUP BY ap.AppraisalId
    ) ci ON ci.AppraisalId = v.Id
         LEFT JOIN appraisal.Appraisals cur ON cur.Id = v.Id
         LEFT JOIN appraisal.Appraisals prev ON prev.Id = cur.PrevAppraisalId
         OUTER APPLY (
    SELECT CollateralType = STRING_AGG(x.PropertyType, ', ')
    FROM (SELECT DISTINCT ap2.PropertyType
          FROM appraisal.AppraisalProperties ap2
          WHERE ap2.AppraisalId = v.Id) x
    ) ct
WHERE ci.ProgressPct < 100;
