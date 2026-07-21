-- RCAS008 — รายงานประเมินผลคุณภาพการให้บริการของ External Appraisal Company
-- Service-quality scores per external appraisal company.
-- Layered on vw_AppraisalEvaluationList (composite score) + AppraisalEvaluations (per-criterion ratings).
CREATE
OR ALTER VIEW reporting.vw_RCAS008_ServiceQuality
AS
SELECT el.AppraisalId           AS Id,
       el.AppraisalNumber,
       el.AppraiserCompanyName  AS AppraisalCompany,
       a.CompletedAt            AS ApprovedDate,
       a.BankingSegment,
       el.TotalScore            AS TotalScorePct,
       e.Criteria1Rating        AS ScoreReportQuality,
       e.Criteria2Rating        AS ScoreDeliveryTime,
       e.Criteria3Rating        AS ScorePersonnel,
       e.Criteria4Rating        AS ScoreResponseTime,
       e.Criteria5Rating        AS ScoreCoordination,
       e.AdditionalComments     AS Remark,
       el.EvaluationStatus,
       -- Appended (not inserted mid-list) so the SELECT order still matches the positional Rcas008Row.
       -- FSD field 3: the bank's internal appraiser on this external book (name, fallback to code).
       COALESCE(NULLIF(v.InternalAppraiserName, N''), v.InternalAppraiserId) AS InternalAppraisalStaff,
       a.Purpose                AS PurposeCode,       -- filter-only (FSD selection criterion "Purpose")
       a.AppraisalType                                -- filter-only (FSD selection criterion "Appraisal type")
FROM appraisal.vw_AppraisalEvaluationList el
         INNER JOIN appraisal.Appraisals a ON a.Id = el.AppraisalId
         LEFT JOIN appraisal.vw_AppraisalList v ON v.Id = el.AppraisalId
         LEFT JOIN appraisal.AppraisalEvaluations e ON e.AppraisalId = el.AppraisalId;
