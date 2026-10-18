-- ============================================================
-- Data fix: settle the SLA status of appraisals that are already closed.
--
-- appraisal.Appraisals.SLAStatus was set to OnTrack at creation and re-evaluated only when work started,
-- so a closed appraisal still carries whatever it had then (almost always OnTrack, even when it was
-- delivered late). Appraisal.Complete / MarkApprovedByCommittee / Cancel now settle it at the moment of
-- closing; this settles the ones that closed before that. The hourly refresh job skips closed rows, so
-- nothing else would ever correct them.
--
-- The rule is AppraisalSla.ClosedStatusAt: Breached when it closed AFTER its due date, else OnTrack (closing
-- exactly on the due date is on time, like IsWithinSLA). Closed at = CompletedAt for a Completed appraisal and
-- CancelledAt for a Cancelled one. A closed row with no due date, or with no close time stored, is left as is.
--
-- Only IsDeleted = 0 rows whose value would actually change, in batches of 4000 so a large table does not
-- escalate to a table lock. Not an EF migration; no audit columns are touched (a derived value, not an edit).
-- Idempotent: a second run changes nothing.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @Batch INT = 1;

WHILE @Batch > 0
BEGIN
    UPDATE TOP (4000) a
    SET    a.[SLAStatus] = x.[NewStatus]
    FROM   [appraisal].[Appraisals] a
           CROSS APPLY (SELECT CASE WHEN a.[Status] = 'Completed' THEN a.[CompletedAt]
                                    ELSE a.[CancelledAt] END AS [ClosedAt]) c
           CROSS APPLY (SELECT CASE WHEN c.[ClosedAt] > a.[SLADueDate] THEN 'Breached'
                                    ELSE 'OnTrack' END AS [NewStatus]) x
    WHERE  a.[IsDeleted] = 0
           AND a.[SLADueDate] IS NOT NULL
           AND a.[Status] IN ('Completed', 'Cancelled')
           AND c.[ClosedAt] IS NOT NULL
           AND ISNULL(a.[SLAStatus], '') <> x.[NewStatus];

    SET @Batch = @@ROWCOUNT;
END
