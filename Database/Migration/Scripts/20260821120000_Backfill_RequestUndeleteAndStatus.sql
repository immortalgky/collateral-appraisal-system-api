/*
    Repairs requests damaged by the "save resets status" bug.

    Root cause (fixed in code alongside this script):
      UpdateRequestCommandHandler set Status back to 'New' on every PUT /requests/{id},
      and UpdateDraftRequestCommandHandler set it back to 'Draft', without looking at the
      current status. After a route-back to appraisal-initiation the maker re-opened the
      request and pressed Save, which demoted an already-submitted request into the
      pre-submission listing (that listing defaults to Status IN ('Draft','New')).
      Users then saw the request as a stale intake item and deleted it, soft-deleting a
      request whose appraisal task was still running.

    RequestedAt is written only by Request.Submit() and is never cleared, so it is a
    reliable marker that a request was handed over to the appraisal workflow, even when
    Status was overwritten afterwards.

    Pre-flight review query (run this first to see what will be touched):

        SELECT r.Id, r.RequestNumber, r.Status, r.RequestedAt, r.CompletedAt,
               r.IsDeleted, r.DeletedAt, r.DeletedBy,
               CASE WHEN EXISTS (SELECT 1 FROM appraisal.Appraisals a WHERE a.RequestId = r.Id)
                    THEN 1 ELSE 0 END AS HasAppraisal
        FROM   request.Requests r
        WHERE  r.RequestedAt IS NOT NULL
          AND  (r.IsDeleted = 1 OR r.Status IN ('New', 'Draft'));
*/

SET NOCOUNT ON;

DECLARE @Undeleted INT = 0;
DECLARE @StatusFixed INT = 0;

-- Step 1: undelete requests that were soft-deleted after they had already been submitted.
-- Delete is only reachable from the request UI and there is no cancel feature, so every
-- submitted-and-deleted row is a victim of the bug above.
UPDATE r
SET    r.IsDeleted = 0,
       r.DeletedAt = NULL,
       r.DeletedBy = NULL
FROM   request.Requests r
WHERE  r.IsDeleted = 1
  AND  r.RequestedAt IS NOT NULL;

SET @Undeleted = @@ROWCOUNT;

-- Step 2: restore the status of requests demoted to New/Draft by a post-submit save.
-- CompletedAt is stamped by Request.Complete(), so a completed request goes back to
-- 'Completed' rather than 'Submitted'.
UPDATE r
SET    r.Status = CASE WHEN r.CompletedAt IS NOT NULL THEN 'Completed' ELSE 'Submitted' END
FROM   request.Requests r
WHERE  r.Status IN ('New', 'Draft')
  AND  r.RequestedAt IS NOT NULL;

SET @StatusFixed = @@ROWCOUNT;

PRINT CONCAT('Requests undeleted: ', @Undeleted, '; requests with status restored: ', @StatusFixed, '.');
