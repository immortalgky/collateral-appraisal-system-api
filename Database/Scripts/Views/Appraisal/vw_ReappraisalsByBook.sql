-- Every reappraisal of an AS400 book (COLLATREV survey number, 'B' prefix dropped), one row per
-- appraisal. The single definition of "this book has been / is being reappraised", read by:
--   * collateral.vw_ReappraisalCandidates       — the "in progress" badge (open rows)
--   * InitiateReappraisalCommandHandler          — refuses a book already under review (open rows),
--                                                  and a nearby book already reviewed (completed rows)
--   * ReappraisalInitiatedIntegrationEventHandler — drops a duplicate message (open rows)
--   * ReappraisalIngestor                        — a consumed book reopens only when none is left
--                                                  standing (non-cancelled rows)
--   * GetReappraisalCandidateByIdQueryHandler    — flags nearby books under review (open rows), hides
--                                                  nearby books already reviewed (completed rows)
-- reporting.vw_RCAS002_ReappraisalDue keeps its own copy: report views read base tables only.
--
-- Three ways an appraisal reappraises a book, one arm and one index each:
--   1. PrevAppraisalId -> the book's own CAS appraisal
--   2. PrevAppraisalNumber = the book (a legacy AS400 "99A…" book outside CAS)
--   3. its request was created by Initiate for the book (Request.ReappraisalBookNumber) — still
--      counts when staff changed the request's prior-appraisal fields before submitting.
-- Arms 1 and 2 require a reappraisal purpose (03, block 09): a construction inspection or an appeal
-- also points back at a book but reviews nothing. Arm 3 is a reappraisal by construction. The same appraisal can come
-- out of two arms; readers only ask "is there one".
--
-- CollateralId: AS400's collateral id of the row the reappraisal was raised for, known only for arm 3
-- (Initiate stamps Request.ReappraisalCollateralId). A block-project book names a whole project and each collateral is a
-- different unit, so readers checking a block-project unit match on it — arms 1 and 2 (a reappraisal
-- raised by hand) cannot say which unit and are not counted for units.
CREATE OR ALTER VIEW appraisal.vw_ReappraisalsByBook
AS
SELECT prev.AppraisalNumber AS BookNumber,
       a.Id AS AppraisalId, a.AppraisalNumber, a.Status, a.GroupTag, a.RequestId, a.CreatedAt, a.CompletedAt,
       CAST(NULL AS nvarchar(50)) AS CollateralId
FROM appraisal.Appraisals prev
JOIN appraisal.Appraisals a ON a.PrevAppraisalId = prev.Id
WHERE prev.IsDeleted = 0
  AND a.IsDeleted = 0
  AND a.Purpose IN ('03', '09')

UNION ALL

SELECT a.PrevAppraisalNumber,
       a.Id, a.AppraisalNumber, a.Status, a.GroupTag, a.RequestId, a.CreatedAt, a.CompletedAt,
       CAST(NULL AS nvarchar(50))
FROM appraisal.Appraisals a
WHERE a.PrevAppraisalNumber IS NOT NULL
  AND a.IsDeleted = 0
  AND a.Purpose IN ('03', '09')

UNION ALL

SELECT r.ReappraisalBookNumber,
       a.Id, a.AppraisalNumber, a.Status, a.GroupTag, a.RequestId, a.CreatedAt, a.CompletedAt,
       r.ReappraisalCollateralId
FROM request.Requests r
JOIN appraisal.Appraisals a ON a.RequestId = r.Id
WHERE r.ReappraisalBookNumber IS NOT NULL
  AND r.IsDeleted = 0
  AND a.IsDeleted = 0;
