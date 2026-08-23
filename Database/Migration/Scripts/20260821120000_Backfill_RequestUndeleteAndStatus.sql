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

    ORDERING -- apply this only AFTER the code fix is deployed. The guards in Request.Delete(),
    Request.MarkAsNew() and Request.Submit() are what stop repaired rows from being demoted and
    deleted all over again. DbUp journals by file name, so if the repair is undone by a later save
    this script will NOT run a second time to fix it. Confirm the deployed build first.

    REVIEW AFTERWARDS -- step 2 undeletes every submitted-and-deleted request. Delete had no guard
    at all before the fix, so a submitted request could also have been removed deliberately
    (duplicate submission, abandoned case, test data). Step 1 records the prior state in
    request.RequestUndeleteAudit before anything is overwritten; rows there with HasAppraisal = 0
    are the ones most likely to have been deleted on purpose. Inspect them and re-delete any that
    should have stayed deleted. Drop the audit table once the repair is signed off.

    Pre-flight review query:

        SELECT r.Id, r.RequestNumber, r.Status, r.RequestedAt, r.CompletedAt,
               r.IsDeleted, r.DeletedAt, r.DeletedBy,
               CASE WHEN EXISTS (SELECT 1 FROM appraisal.Appraisals a WHERE a.RequestId = r.Id)
                    THEN 1 ELSE 0 END AS HasAppraisal
        FROM   request.Requests r
        WHERE  r.RequestedAt IS NOT NULL
          AND  (r.IsDeleted = 1 OR r.Status IN ('New', 'Draft'));
*/

SET NOCOUNT ON;

IF OBJECT_ID('request.RequestUndeleteAudit') IS NULL
    CREATE TABLE request.RequestUndeleteAudit
    (
        RequestId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        RequestNumber  NVARCHAR(255)    NULL,
        PreviousStatus NVARCHAR(10)     NOT NULL,
        RequestedAt    DATETIME2        NULL,
        CompletedAt    DATETIME2        NULL,
        WasDeleted     BIT              NOT NULL,
        DeletedAt      DATETIME2        NULL,
        DeletedBy      NVARCHAR(10)     NULL,
        HasAppraisal   BIT              NOT NULL,
        CapturedAt     DATETIME2        NOT NULL
            CONSTRAINT DF_RequestUndeleteAudit_CapturedAt DEFAULT SYSDATETIME()
    );

-- Step 1: record the prior state of every row the repair is about to touch. Step 2 nulls
-- DeletedAt/DeletedBy, which would otherwise erase all trace of which rows were undeleted.
INSERT INTO request.RequestUndeleteAudit
    (RequestId, RequestNumber, PreviousStatus, RequestedAt, CompletedAt,
     WasDeleted, DeletedAt, DeletedBy, HasAppraisal)
SELECT r.Id, r.RequestNumber, r.Status, r.RequestedAt, r.CompletedAt,
       r.IsDeleted, r.DeletedAt, r.DeletedBy,
       CASE WHEN EXISTS (SELECT 1 FROM appraisal.Appraisals a WHERE a.RequestId = r.Id)
            THEN 1 ELSE 0 END
FROM   request.Requests r
WHERE  r.RequestedAt IS NOT NULL
  AND  (r.IsDeleted = 1 OR r.Status IN ('New', 'Draft'))
  AND  NOT EXISTS (SELECT 1 FROM request.RequestUndeleteAudit a WHERE a.RequestId = r.Id);

DECLARE @Captured INT = @@ROWCOUNT;

-- Step 2: undelete requests that were soft-deleted after they had already been submitted.
UPDATE r
SET    r.IsDeleted = 0,
       r.DeletedAt = NULL,
       r.DeletedBy = NULL
FROM   request.Requests r
WHERE  r.IsDeleted = 1
  AND  r.RequestedAt IS NOT NULL;

DECLARE @Undeleted INT = @@ROWCOUNT;

-- Step 3: restore the status of requests demoted to New/Draft by a post-submit save.
-- Request only ever reaches Draft, New, Submitted and Completed today -- Assigned, InProgress and
-- Cancelled are declared on RequestStatus but nothing ever writes them -- so collapsing to these
-- two targets is complete. Revisit if a new status starts being written.
UPDATE r
SET    r.Status = CASE WHEN r.CompletedAt IS NOT NULL THEN 'Completed' ELSE 'Submitted' END
FROM   request.Requests r
WHERE  r.Status IN ('New', 'Draft')
  AND  r.RequestedAt IS NOT NULL;

DECLARE @StatusFixed INT = @@ROWCOUNT;

PRINT CONCAT('Captured to request.RequestUndeleteAudit: ', @Captured,
             '; requests undeleted: ', @Undeleted,
             '; requests with status restored: ', @StatusFixed, '.');
