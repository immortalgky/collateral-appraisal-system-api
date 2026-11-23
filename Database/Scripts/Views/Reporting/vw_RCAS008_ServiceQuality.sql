-- RCAS008 — รายงานประเมินผลคุณภาพการให้บริการของ External Appraisal Company
-- Service-quality scores per external appraisal company.
-- Layered on vw_AppraisalEvaluationList (composite score) + AppraisalEvaluations (per-criterion ratings).
CREATE
OR ALTER VIEW reporting.vw_RCAS008_ServiceQuality
AS
SELECT el.AppraisalId           AS Id,
       el.AppraisalNumber,
       COALESCE(NULLIF(el.AppraiserCompanyNameLocal, N''), el.AppraiserCompanyName)  AS AppraisalCompany,
       el.AppraiserCompanyName  AS AppraisalCompanyEn,   -- filter key: stays English so a company filter matches in either language
       a.CompletedAt            AS ApprovedDate,
       a.BankingSegment,
       el.TotalScorePct,
       e.Criteria1Rating        AS ScoreReportQuality,
       e.Criteria2Rating        AS ScoreDeliveryTime,
       e.Criteria3Rating        AS ScorePersonnel,
       e.Criteria4Rating        AS ScoreResponseTime,
       e.Criteria5Rating        AS ScoreCoordination,
       e.AdditionalComments     AS Remark,
       el.EvaluationStatus,
       -- Appended (not inserted mid-list) so the SELECT order still matches the positional Rcas008Row.
       -- FSD field 3: the bank's internal appraiser on this external book: "First Last (usercode)",
       -- bare code when no name resolves, NULL when there is no internal appraiser.
       CASE
           WHEN NULLIF(v.InternalAppraiserId, N'') IS NULL THEN NULL
           WHEN nm.StaffName IS NULL THEN NULLIF(v.InternalAppraiserId, N'')
           ELSE nm.StaffName + N' (' + NULLIF(v.InternalAppraiserId, N'') + N')'
           END                  AS InternalAppraisalStaff,
       a.Purpose                AS PurposeCode,       -- filter-only (FSD selection criterion "Purpose")
       a.AppraisalType,                               -- filter-only (FSD selection criterion "Appraisal type")
       -- Per-criterion remarks, appended last for the same reason (positional Rcas008Row).
       e.Criteria1Remark        AS RemarkReportQuality,
       e.Criteria2Remark        AS RemarkDeliveryTime,
       e.Criteria3Remark        AS RemarkPersonnel,
       e.Criteria4Remark        AS RemarkResponseTime,
       e.Criteria5Remark        AS RemarkCoordination,
       -- Display name for the Banking Segment column; BankingSegment above stays the code (filter key).
       COALESCE(bs.Description, a.BankingSegment) AS BankingSegmentName,
       e.Note                   AS Note
FROM appraisal.vw_AppraisalEvaluationList el
         INNER JOIN appraisal.Appraisals a ON a.Id = el.AppraisalId
         LEFT JOIN appraisal.vw_AppraisalList v ON v.Id = el.AppraisalId
         LEFT JOIN appraisal.AppraisalEvaluations e ON e.AppraisalId = el.AppraisalId
         LEFT JOIN auth.AspNetUsers su ON su.NormalizedUserName = UPPER(v.InternalAppraiserId)
         -- Same "First Last" expression as vw_RCAS001; the list view's own name stays as a fallback.
         OUTER APPLY (SELECT COALESCE(
                 NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(su.FirstName, N''), N' ', NULLIF(su.LastName, N'')))), N''),
                 NULLIF(v.InternalAppraiserName, N'')) AS StaffName) nm
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'BankingSegment' AND [Language] = 'EN' AND Code = a.BankingSegment) bs;
