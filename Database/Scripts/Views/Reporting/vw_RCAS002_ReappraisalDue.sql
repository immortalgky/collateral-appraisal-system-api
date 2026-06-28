-- RCAS002 — รายงานการครบกำหนดทบทวนหลักประกันตามประเภท
-- Collateral review-due by type, from the AS400-sourced reappraisal candidates.
--
-- Reads the BASE table collateral.ReappraisalCandidates (not collateral.vw_ReappraisalCandidates) on
-- purpose: repeatable view scripts deploy in folder-alphabetical order, so a sibling view may not
-- exist yet on a fresh deploy. The base table exists after EF migrations, and
-- appraisal.vw_AppraisalList (folder "Appraisal") sorts before this view.
-- NOTE: the reappraisal vertical moved request -> collateral schema; this view follows it.
-- AppraisalDate / NextValuationDate are derived here the same way the Request view derives them.
--
-- CODE -> DESCRIPTION RESOLUTION:
--   * ReviewType : AS400 review code 1/2/3 -> readable label via CASE (documented enum:
--                  1 = Normal, 2 = Before Stage 3, 3 = Stage 3). COALESCE-style fallback to the
--                  raw value keeps any unmapped code visible.
--   The remaining AS400-proprietary codes (CollateralCategory, Stage, IBGRetail) are passed through
--   unchanged: they are not bank parameter codes and have no parameter.Parameters group, so they
--   need a business-supplied code list before they can be resolved.
CREATE
OR ALTER VIEW reporting.vw_RCAS002_ReappraisalDue
AS
SELECT c.Id,
       CASE c.ReviewType
           WHEN '1' THEN 'Normal'
           WHEN '2' THEN 'Before Stage 3'
           WHEN '3' THEN 'Stage 3'
           ELSE c.ReviewType
       END                                 AS ReviewType,
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
FROM collateral.ReappraisalCandidates c
         OUTER APPLY (
    SELECT TOP 1 al.AppointmentDateTime
    FROM appraisal.Appraisals a
             INNER JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
    WHERE a.AppraisalNumber = c.SurveyNumber
    ORDER BY al.AppointmentDateTime DESC
    ) la
WHERE c.Status <> 'Deleted';
