-- AS400 periodical reappraisal requests that are still WAITING — created by Initiate and not yet in
-- the workflow. The single definition of "waiting", read by:
--   * collateral.vw_ReappraisalCandidates     — the "in progress" badge while no appraisal exists
--   * InitiateReappraisalCommandHandler        — refuses a book that already has one
--   * ReappraisalInitiatedIntegrationEventHandler — drops a duplicate message for the same book
--   * ReappraisalIngestor                      — a consumed book is not reopened while one waits
--
-- Waiting = unsubmitted (Draft / New), or submitted with its appraisal not created yet by the
-- workflow — without the second arm there is a window after Submit where nothing marks the book.
-- The second arm has no time limit: the book is consumed on submit, so if the workflow never creates
-- the appraisal, this request is the only thing keeping the next COLLATREV file from reopening the
-- book and Initiate from raising a second request. It stops counting when the request is cancelled.
--
-- One row per request, keyed by BOOK: Request.ReappraisalBookNumber, the normalised survey number
-- Initiate stamps with the GroupTag — the same number the candidate list keys on. A system field, not
-- the form's prior-appraisal fields: staff can clear those, and the book must stay "waiting" anyway.
-- Readers match on the book alone: one request covers every collateral of the book. The filtered
-- index IX_Request_ReappraisalBookNumber makes each lookup a seek.
CREATE OR ALTER VIEW request.vw_WaitingReappraisalRequests
AS
SELECT
    r.Id                    AS RequestId,
    r.RequestNumber,
    r.GroupTag,
    r.CreatedAt,
    -- AS400's collateral id of the row the request reviews (a key on the AS400 side only) — what tells
    -- the units of one block project apart.
    r.ReappraisalCollateralId AS CollateralId,
    r.ReappraisalBookNumber AS BookNumber
FROM request.Requests r
-- Created by Initiate, the only writer of ReappraisalBookNumber.
WHERE r.ReappraisalBookNumber IS NOT NULL
  AND r.IsDeleted = 0
  AND (r.Status IN ('Draft', 'New')
       OR (r.Status = 'Submitted'
           AND NOT EXISTS (SELECT 1 FROM appraisal.Appraisals a WHERE a.RequestId = r.Id)));
