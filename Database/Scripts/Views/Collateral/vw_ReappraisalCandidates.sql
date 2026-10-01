CREATE
OR ALTER
VIEW collateral.vw_ReappraisalCandidates AS
-- One row per BOOK (CollateralId + normalised survey number): later COLLATREV files refresh the row
-- instead of adding one, so this view never has to collapse months. Every status is returned — the
-- list query picks Pending ("to do") or Deleted ("not reviewing this round").
--
-- The latest file date across the table. A Pending/Deleted book whose LastSeenFileDate is older is no
-- longer on AS400's due list: IsInLatestFile = 0, and the list hides it. Consumed books are not stamped
-- when AS400 repeats them, so IsInLatestFile means nothing for them.
WITH latest AS (
    -- The newest COLLATREV file ingested (collateral.vw_ReappraisalLatestFile is the one definition).
    SELECT FileDate FROM collateral.vw_ReappraisalLatestFile
)
SELECT
    c.Id,
    c.SourceFileName,
    c.SourceFileDate,
    c.EffectiveDate,
    c.IngestedAt,
    c.Status,
    c.ReviewType,
    c.ReviewDate,
    c.SourceFileDate                                    AS FirstSeenFileDate,
    COALESCE(c.LastSeenFileDate, c.SourceFileDate)      AS LastSeenFileDate,
    CAST(CASE WHEN COALESCE(c.LastSeenFileDate, c.SourceFileDate) = latest.FileDate
              THEN 1 ELSE 0 END AS BIT)                 AS IsInLatestFile,
    -- AppraisalDate / RemainingDay / DaysSinceLastAppraisal are all derived from the matched
    -- in-system appraisal's appraisal date — ValuationAnalyses.ValuationDate, falling back to the
    -- latest non-cancelled appointment (see OUTER APPLY `last_appr` below). A book with no CAS
    -- appraisal (legacy AS400 "99" series, or unmatched) falls back to the legacy listing, then to
    -- the valuation date AS400 sent — otherwise it silently drops out of the due schedule:
    --   AppraisalDate            = AppraisalDate
    --   RemainingDay             = (AppraisalDate + 5 years) − today
    --   DaysSinceLastAppraisal   = today − AppraisalDate
    -- NULL when SurveyNumber doesn't resolve to any in-system appraisal. Note c.ValuationDate
    -- below is a DIFFERENT field — the AS400 inbound value off the Collatrev file, not ours.
    appr.AppraisalDate                                                                     AS AppraisalDate,
    DATEDIFF(DAY,
        CAST(GETDATE() AS DATE),
        DATEADD(YEAR, 5, appr.AppraisalDate))                                              AS RemainingDay,
    DATEDIFF(DAY,
        appr.AppraisalDate,
        CAST(GETDATE() AS DATE))                                                           AS DaysSinceLastAppraisal,
    -- Where the prior book lives: 'CAS' (an appraisal in this system), 'AS400Legacy' (the bank's
    -- legacy listing — a 99A… book), or 'Unknown'. The FE labels the row with it.
    CASE WHEN last_appr.AppraisalDate IS NOT NULL OR prev_cas.Id IS NOT NULL THEN 'CAS'
         WHEN legacy.ValuationDate IS NOT NULL THEN 'AS400Legacy'
         ELSE 'Unknown'
    END                                                                                    AS PriorAppraisalSource,
    c.CollateralId,
    c.SurveyNumber          AS OldAppraisalReportNumber,
    bk.Number               AS NormalizedSurveyNumber,
    -- A unit of a block project: reviewed per collateral (see ReappraisalCandidate.IsBlockUnit).
    c.IsBlockUnit,
    c.CollateralCode,
    c.CollateralCategory,
    c.CollateralName,
    c.CollateralAddress,
    c.CifNumber,
    c.CifName               AS CustomerName,
    c.AoCode,
    c.AoName,
    c.TitleNumber,
    c.CurrentValue,
    c.ValuationDate,
    c.InternalExternal,
    c.BusinessSize,
    c.BusinessSizeDesc,
    c.MortgageAmount,
    c.PastDueDay,
    c.ApplicationNumber,
    c.FacilityCode,
    c.FacilitySequence,
    c.CpNumber,
    c.CarCode,
    c.FacilityLimit,
    c.FlagLessAge4Y,
    c.FlagGreaterAge4Y,
    c.CountAgeingDate,
    c.CollateralDescription,
    c.ExternalValuerName,
    c.InternalValuerName,
    c.SllOver100M,
    c.SllDescription,
    -- Trailing extension fields (pos 641–660 in the input file).
    c.Stage,
    c.IBGRetail,
    c.[Group],
    c.EffectiveDateAppraisal,
    c.Latitude,
    c.Longitude,
    -- "In Progress" indicator. TRUE when either
    --   * an in-flight (non-terminal, non-deleted) reappraisal Appraisal points back at this book —
    --     by PrevAppraisalId for a CAS book, by PrevAppraisalNumber for a legacy AS400 book; or
    --   * a reappraisal Request for this book is still waiting (request.vw_WaitingReappraisalRequests).
    --     Initiate only CREATES the request; no Appraisal exists until staff submit it, and without
    --     this arm the book would look free and could be initiated again and again.
    -- OpenAppraisal* carry the open appraisal (badge "→ <number>"); OpenRequest* the waiting request.
    CAST(CASE WHEN open_req.OpenAppraisalId IS NOT NULL OR open_draft.OpenRequestId IS NOT NULL
              THEN 1 ELSE 0 END AS BIT) AS HasOpenAppraisal,
    open_req.OpenAppraisalId         AS OpenAppraisalId,
    open_req.OpenAppraisalNumber     AS OpenAppraisalNumber,
    COALESCE(open_req.OpenAppraisalGroupTag, open_draft.OpenRequestGroupTag) AS OpenAppraisalGroupTag,
    open_draft.OpenRequestId         AS OpenRequestId,
    open_draft.OpenRequestNumber     AS OpenRequestNumber
FROM collateral.ReappraisalCandidates c
CROSS JOIN latest
-- The book's number as CAS stores it ('B' prefix dropped). Written at ingest, and on older rows by
-- 20260928120000_DataFix_BackfillReappraisalCandidateBookKey.sql before any view is created.
CROSS APPLY (SELECT c.NormalizedSurveyNumber AS Number) bk
-- The prior book, when it is an appraisal in this system.
OUTER APPLY (
    SELECT TOP 1 prev.Id
    FROM appraisal.Appraisals prev
    WHERE prev.AppraisalNumber = bk.Number
      AND prev.IsDeleted = 0
    ORDER BY prev.Id  -- same pick as Initiate (MIN(Id)) and the detail page
) prev_cas
-- An open reappraisal of the book (appraisal.vw_ReappraisalsByBook is the one definition of "a
-- reappraisal of this book"). OUTER APPLY collapses history to the most recent.
OUTER APPLY (
    SELECT TOP 1
        rb.AppraisalId     AS OpenAppraisalId,
        rb.AppraisalNumber AS OpenAppraisalNumber,
        rb.GroupTag        AS OpenAppraisalGroupTag
    FROM appraisal.vw_ReappraisalsByBook rb
    WHERE rb.BookNumber = bk.Number
      AND rb.Status NOT IN ('Completed', 'Cancelled')
      -- A block-project unit: only a reappraisal raised for this collateral (this unit).
      AND (c.IsBlockUnit = 0 OR rb.CollateralId = c.CollateralId)
    ORDER BY rb.CreatedAt DESC
) open_req
-- A reappraisal request for this book that Initiate created and the workflow has not picked up yet
-- (request.vw_WaitingReappraisalRequests is the one definition of "waiting").
OUTER APPLY (
    SELECT TOP 1
        w.RequestId     AS OpenRequestId,
        w.RequestNumber AS OpenRequestNumber,
        w.GroupTag      AS OpenRequestGroupTag
    FROM request.vw_WaitingReappraisalRequests w
    -- By book alone: one request covers every collateral of the book (same grain as open_req) —
    -- except a block-project unit, whose request is for its own collateral.
    WHERE w.BookNumber = bk.Number
      AND (c.IsBlockUnit = 0 OR w.CollateralId = c.CollateralId)
    ORDER BY w.CreatedAt DESC
) open_draft
-- Last in-system appraisal date for this candidate (matched via SurveyNumber = AppraisalNumber).
-- Drives AppraisalDate / RemainingDay / DaysSinceLastAppraisal above. NULL when unmatched.
-- a.CompletedAt is the last fallback: a legacy/migrated appraisal can have neither a
-- ValuationAnalyses row nor an Appointment row, and without it AppraisalDate is NULL, so
-- RemainingDay / DaysSinceLastAppraisal are NULL too and the candidate silently drops out of the
-- reappraisal-due schedule despite having a perfectly good completion date.
OUTER APPLY (
    SELECT TOP 1 COALESCE(va.ValuationDate, al.AppointmentDateTime, a.CompletedAt) AS AppraisalDate
    FROM appraisal.Appraisals a
    JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
    WHERE a.AppraisalNumber = bk.Number
      AND a.IsDeleted = 0
    ORDER BY COALESCE(va.ValuationDate, al.AppointmentDateTime, a.CompletedAt) DESC
) last_appr
-- The bank's legacy listing for a 99A… book that never existed in CAS. ApplicationId is char-padded;
-- `=` ignores trailing spaces, so no RTRIM (which would stop the comparison being sargable).
-- The book's LATEST valuation there: this drives the next-due clock (the regulatory origination is
-- the one that takes the earliest).
OUTER APPLY (
    SELECT MAX(l.ValuationDate) AS ValuationDate
    FROM appraisal.AS400ReportListing l
    WHERE l.ApplicationId = bk.Number
) legacy
CROSS APPLY (
    SELECT CAST(COALESCE(last_appr.AppraisalDate, legacy.ValuationDate, c.ValuationDate) AS DATE) AS AppraisalDate
) appr
