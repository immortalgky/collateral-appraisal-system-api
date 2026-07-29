-- RCAS002 — รายงานการครบกำหนดทบทวนหลักประกันตามประเภท
-- Collateral review-due by type, from the AS400-sourced reappraisal candidates.
--
-- Reads the BASE table collateral.ReappraisalCandidates (not collateral.vw_ReappraisalCandidates) on
-- purpose: repeatable view scripts deploy in folder-alphabetical order, so a sibling view may not
-- exist yet on a fresh deploy. The base table exists after EF migrations, and
-- appraisal.vw_AppraisalList (folder "Appraisal") sorts before this view.
-- NOTE: the reappraisal vertical moved request -> collateral schema; this view follows it.
-- NextValuationDate / RemainingDays derive from the matched in-system appraisal's appraisal date
-- (+5 years) — ValuationAnalyses.ValuationDate, falling back to the latest non-cancelled
-- appointment — the same way vw_ReappraisalCandidates does.
-- c.ValuationDate is a DIFFERENT field: the AS400 inbound value off the Collatrev file.
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
       DATEADD(YEAR, 5, CAST(la.AppraisalDate AS DATE)) AS NextValuationDate,
       DATEDIFF(DAY,
                CAST(GETDATE() AS DATE),
                DATEADD(YEAR, 5, CAST(la.AppraisalDate AS DATE))) AS RemainingDays,
       -- Appended (not inserted mid-list) so the SELECT order still matches the positional Rcas002Row.
       c.ReviewType                        AS ReviewTypeCode -- raw 1/2/3: filter binds the code, sort follows code order
FROM collateral.ReappraisalCandidates c
         -- a.CompletedAt is the last fallback: a legacy/migrated appraisal can have neither a
         -- ValuationAnalyses row nor an Appointment row, and without it NextValuationDate and
         -- RemainingDays are NULL, so the collateral drops out of the reappraisal-due report
         -- despite having a perfectly good completion date to anchor the +5 years on.
         OUTER APPLY (
    SELECT TOP 1 COALESCE(va.ValuationDate, al.AppointmentDateTime, a.CompletedAt) AS AppraisalDate
    FROM appraisal.Appraisals a
             INNER JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
             LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
    WHERE a.AppraisalNumber = c.SurveyNumber
    ORDER BY COALESCE(va.ValuationDate, al.AppointmentDateTime, a.CompletedAt) DESC
    ) la
WHERE c.Status <> 'Deleted';
