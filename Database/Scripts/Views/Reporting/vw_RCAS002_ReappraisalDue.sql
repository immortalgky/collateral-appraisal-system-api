-- RCAS002 — รายงานการครบกำหนดทบทวนหลักประกันตามประเภท
-- Collateral review-due by type, from the AS400-sourced reappraisal candidates.
--
-- Reads the BASE table request.ReappraisalCandidates (not request.vw_ReappraisalCandidates) on
-- purpose: repeatable view scripts deploy in folder-alphabetical order, and "Reporting" sorts
-- before "Request", so the Request view may not exist yet on a fresh deploy. The base table exists
-- after EF migrations, and appraisal.vw_AppraisalList (folder "Appraisal") sorts before this view.
-- AppraisalDate / NextValuationDate are derived here the same way the Request view derives them.
CREATE
OR ALTER VIEW reporting.vw_RCAS002_ReappraisalDue
AS
SELECT c.Id,
       c.ReviewType,
       c.Stage,
       c.SurveyNumber                      AS AppraisalNumber,
       CAST(NULL AS NVARCHAR(50))          AS PreviousAppraisalNumber, -- prior cycle not tracked yet
       c.CollateralCode                    AS CollateralNumber,
       c.CifNumber,
       c.CifName                           AS CustomerName,
       c.FacilityLimit                     AS ApplyLimitAmount,
       c.CollateralCategory                AS CollateralType,
       c.TitleNumber                       AS TitleDeedNumber,
       c.IBGRetail                         AS BankingSegment,
       c.ExternalValuerName                AS AppraisalCompany,
       c.InternalValuerName                AS InternalAppraisalStaff,
       c.CurrentValue                      AS OldAppraisalValue,
       c.PastDueDay,
       c.ValuationDate,
       DATEADD(YEAR, 5, CAST(la.AppointmentDateTime AS DATE)) AS NextValuationDate,
       DATEDIFF(DAY,
                CAST(GETDATE() AS DATE),
                DATEADD(YEAR, 5, CAST(la.AppointmentDateTime AS DATE))) AS RemainingDays
FROM request.ReappraisalCandidates c
         OUTER APPLY (
    SELECT TOP 1 al.AppointmentDateTime
    FROM appraisal.Appraisals a
             INNER JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
    WHERE a.AppraisalNumber = c.SurveyNumber
    ORDER BY al.AppointmentDateTime DESC
    ) la
WHERE c.Status <> 'Deleted';
