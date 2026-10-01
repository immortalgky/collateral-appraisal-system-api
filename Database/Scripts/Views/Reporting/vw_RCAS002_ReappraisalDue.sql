-- RCAS002 — รายงานการครบกำหนดทบทวนหลักประกันตามประเภท
-- Collateral review-due by type, from the AS400-sourced reappraisal candidates.
--
-- THE WHOLE HISTORY, ONE ROW PER BOOK. Which books AS400 has ever sent for review, which were
-- reviewed (and when), and which are still waiting:
--   * reviewed (Consumed)          — always listed, even after the book has left AS400's file, with the
--                                    reappraisal it produced: number, submitted, completed, status
--   * waiting (Pending) and "not reviewing this round" (Deleted)
--                                  — listed only while on AS400's LATEST file; a book that dropped off
--                                    is no longer due
-- Rows written before books were deduplicated can repeat a book across months; `ranked` keeps one
-- (the most recently seen, a Consumed one on a tie — the same rule as the ingestor, so a reopened book
-- reports as waiting again).
--
-- Reads the BASE table collateral.ReappraisalCandidates (not collateral.vw_ReappraisalCandidates) on
-- purpose: repeatable view scripts deploy in folder-alphabetical order, so a sibling view may not
-- exist yet on a fresh deploy. The base table exists after EF migrations, and
-- appraisal.vw_AppraisalList (folder "Appraisal") sorts before this view.
-- NOTE: the reappraisal vertical moved request -> collateral schema; this view follows it.
-- NextValuationDate / RemainingDays derive from the matched in-system appraisal's appraisal date
-- (+5 years) — ValuationAnalyses.ValuationDate, falling back to the latest non-cancelled
-- appointment, then the legacy listing / AS400's own valuation date for a book not in CAS — the
-- same way vw_ReappraisalCandidates does.
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
WITH latest AS (
    -- The newest COLLATREV file actually ingested. Read from the file ledger, not only from the rows:
    -- Consumed books are skipped without being stamped, so a file that lists only reviewed books
    -- would otherwise never register as the latest and dropped books would stay listed. The rows'
    -- own maximum still counts, for files ingested before the ledger existed.
    -- A copy of collateral.vw_ReappraisalLatestFile (report views read base tables only): keep the two identical.
    SELECT CASE WHEN c.FileDate IS NULL OR f.FileDate > c.FileDate THEN f.FileDate ELSE c.FileDate END AS FileDate
    FROM (SELECT MAX(l.FileDate) AS FileDate
          FROM integration.InboundFileLogs l
          WHERE l.InterfaceCode = 'REAPPRAISAL' AND l.Status = 'Succeeded') f
    CROSS JOIN (SELECT MAX(LastSeenFileDate) AS FileDate
                FROM collateral.ReappraisalCandidates) c
),
ranked AS (
    SELECT rc.*,
           rc.NormalizedSurveyNumber AS BookNumber,
           -- The ingestor's rule: the most recently seen copy, Consumed on a tie. The one-time script
           -- consumed every stale copy of a reviewed book, so the newest copy is the book's real state
           -- — a reopened book reports as waiting (and drops out with the file, like any other).
           ROW_NUMBER() OVER (
               PARTITION BY rc.CollateralId, rc.NormalizedSurveyNumber
               ORDER BY COALESCE(rc.LastSeenFileDate, rc.SourceFileDate) DESC,
                        CASE WHEN rc.Status = 'Consumed' THEN 0 ELSE 1 END) AS rn
    FROM collateral.ReappraisalCandidates rc
)
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
       DATEADD(YEAR, 5, appr.AppraisalDate) AS NextValuationDate,
       DATEDIFF(DAY,
                CAST(GETDATE() AS DATE),
                DATEADD(YEAR, 5, appr.AppraisalDate)) AS RemainingDays,
       -- Appended (not inserted mid-list) so the SELECT order still matches the positional Rcas002Row.
       c.ReviewType                        AS ReviewTypeCode, -- raw 1/2/3: filter binds the code, sort follows code order
       CASE c.Status
           WHEN 'Consumed' THEN N'ดำเนินการแล้ว'
           WHEN 'Deleted'  THEN N'ไม่ทบทวนรอบนี้'
           ELSE N'รอดำเนินการ'
       END                                 AS ReviewStatus,
       na.AppraisalNumber                  AS NewAppraisalNumber,
       na.SubmittedAt                      AS NewAppraisalSubmittedAt,
       na.CompletedAt                      AS NewAppraisalCompletedAt,
       na.Status                           AS NewAppraisalStatus,
       c.Status                            AS ReviewStatusCode
FROM ranked c
         CROSS JOIN latest
         -- a.CompletedAt is the last fallback: a legacy/migrated appraisal can have neither a
         -- ValuationAnalyses row nor an Appointment row, and without it NextValuationDate and
         -- RemainingDays are NULL, so the collateral drops out of the reappraisal-due report
         -- despite having a perfectly good completion date to anchor the +5 years on.
         OUTER APPLY (
    SELECT TOP 1 COALESCE(va.ValuationDate, al.AppointmentDateTime, a.CompletedAt) AS AppraisalDate
    FROM appraisal.Appraisals a
             INNER JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
             LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
    WHERE a.AppraisalNumber = c.BookNumber
      AND a.IsDeleted = 0
    ORDER BY COALESCE(va.ValuationDate, al.AppointmentDateTime, a.CompletedAt) DESC
    ) la
         -- A book with no CAS appraisal (legacy AS400 "99A…", or unmatched) falls back to the bank's
         -- legacy listing, then to the valuation date AS400 sent — same rule as vw_ReappraisalCandidates.
         OUTER APPLY (
    SELECT MAX(l.ValuationDate) AS ValuationDate  -- latest valuation drives the next-due date
    FROM appraisal.AS400ReportListing l
    WHERE l.ApplicationId = c.BookNumber
    ) legacy
         CROSS APPLY (
    SELECT CAST(COALESCE(la.AppraisalDate, legacy.ValuationDate, c.ValuationDate) AS DATE) AS AppraisalDate
    ) appr
         -- The reappraisal this book produced, newest non-cancelled first. Two arms, one key each:
         -- PrevAppraisalId for a book that is a CAS appraisal, PrevAppraisalNumber for a legacy
         -- AS400 book (never both on one row). Submitted = when the request was sent into the workflow.
         OUTER APPLY (
    SELECT TOP 1 n.AppraisalNumber, r.RequestedAt AS SubmittedAt, n.CompletedAt, n.Status
    FROM (
        SELECT na1.AppraisalNumber, na1.CompletedAt, na1.Status, na1.RequestId, na1.CreatedAt
        FROM appraisal.Appraisals prev
                 JOIN appraisal.Appraisals na1 ON na1.PrevAppraisalId = prev.Id
        WHERE prev.AppraisalNumber = c.BookNumber
          AND prev.IsDeleted = 0
          -- A block-project unit (each collateral a different unit) only through its own request below.
          AND c.IsBlockUnit = 0
          -- Reappraisals only (purpose 03, block 09) — same rule as the ingestor. A construction
          -- inspection or appeal also points back at the book but is not the review of it.
          AND na1.Purpose IN ('03', '09')
          AND na1.IsDeleted = 0
        UNION ALL
        SELECT na2.AppraisalNumber, na2.CompletedAt, na2.Status, na2.RequestId, na2.CreatedAt
        FROM appraisal.Appraisals na2
        WHERE na2.PrevAppraisalNumber = c.BookNumber
          AND c.IsBlockUnit = 0
          AND na2.Purpose IN ('03', '09')
          AND na2.IsDeleted = 0
        UNION ALL
        -- The appraisal of a request Initiate created for this book, even if staff changed its
        -- prior-appraisal fields before submitting (Request.ReappraisalBookNumber is a system field).
        SELECT na3.AppraisalNumber, na3.CompletedAt, na3.Status, na3.RequestId, na3.CreatedAt
        FROM request.Requests r3
        JOIN appraisal.Appraisals na3 ON na3.RequestId = r3.Id
        WHERE r3.ReappraisalBookNumber = c.BookNumber
          AND (c.IsBlockUnit = 0 OR r3.ReappraisalCollateralId = c.CollateralId)
          AND r3.IsDeleted = 0
          AND na3.IsDeleted = 0
    ) n
             LEFT JOIN request.Requests r ON r.Id = n.RequestId
    ORDER BY CASE WHEN n.Status = 'Cancelled' THEN 1 ELSE 0 END, n.CreatedAt DESC
    ) na
WHERE c.rn = 1
  AND (c.Status = 'Consumed'
       OR COALESCE(c.LastSeenFileDate, c.SourceFileDate) = latest.FileDate);
