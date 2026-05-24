-- Per-appraisal committee approval roster with each approver's vote status.
--
-- Answers "who still needs to approve this appraisal" for all three tiers
-- (sub-committee, committee without meeting, committee with meeting). Every tier votes
-- through the workflow ApprovalActivity, so the roster is uniform:
--   * Pending  = an open per-member workflow.PendingTasks row for the 'pending-approval'
--                activity (created when the activity starts; removed when the member votes).
--   * Voted    = a workflow.ApprovalVotes row in the latest execution of that activity.
--
-- NOTE for tier 3 (committee with meeting): the per-member approval tasks/votes only exist
-- once the meeting releases the item into 'pending-approval'. While the appraisal is still
-- waiting in the meeting, no approval rows exist here yet (the expected attendees at that
-- stage live in workflow.vw_MeetingRoster).
CREATE
OR ALTER
VIEW workflow.vw_AppraisalApprovalStatus AS
WITH latest_exec AS (
    -- Newest approval execution per (workflow instance, activity) — isolates the current
    -- round so prior route-back rounds' votes are excluded.
    SELECT av.ActivityExecutionId,
           ROW_NUMBER() OVER (PARTITION BY av.WorkflowInstanceId, av.ActivityId
                              ORDER BY MAX(av.VotedAt) DESC) AS rn
    FROM workflow.ApprovalVotes av
    GROUP BY av.WorkflowInstanceId, av.ActivityId, av.ActivityExecutionId
),
roster AS (
    -- Approvers who have NOT voted yet in the current round.
    SELECT pt.WorkflowInstanceId,
           pt.ActivityId,
           pt.AssignedTo                AS MemberUsername,
           CAST(NULL AS NVARCHAR(50))   AS MemberRole,
           'Pending'                    AS VoteStatus,
           CAST(NULL AS DATETIME2)      AS VotedAt,
           CAST(NULL AS NVARCHAR(1000)) AS Comments,
           pt.DueAt,
           pt.SlaStatus
    FROM workflow.PendingTasks pt
    WHERE pt.ActivityId = 'pending-approval'

    UNION ALL

    -- Approvers who HAVE voted in the latest round.
    SELECT av.WorkflowInstanceId,
           av.ActivityId,
           av.Member                    AS MemberUsername,
           av.MemberRole,
           av.Vote                      AS VoteStatus,
           av.VotedAt,
           av.Comments,
           CAST(NULL AS DATETIME2)      AS DueAt,
           CAST(NULL AS NVARCHAR(20))   AS SlaStatus
    FROM workflow.ApprovalVotes av
             JOIN latest_exec le
                  ON le.ActivityExecutionId = av.ActivityExecutionId AND le.rn = 1
)
SELECT a.Id                                                                                AS AppraisalId,
       a.AppraisalNumber,
       -- Tier derived from the committee code: post-approval from the appraisal's stored
       -- ApprovedByCommittee code, in-flight from the pending approval task's stamped code.
       COALESCE(
           CASE a.ApprovedByCommittee
               WHEN 'SUB_COMMITTEE' THEN 1 WHEN 'COMMITTEE' THEN 2 WHEN 'COMMITTEE_WITH_MEETING' THEN 3 END,
           CASE ptc.CommitteeCode
               WHEN 'SUB_COMMITTEE' THEN 1 WHEN 'COMMITTEE' THEN 2 WHEN 'COMMITTEE_WITH_MEETING' THEN 3 END
       )                                                                                   AS ApprovalTier,
       rv.MeetingId,
       rvm.MeetingNo                                                                       AS MeetingReference,
       roster.WorkflowInstanceId,
       roster.ActivityId,
       roster.MemberUsername                                                               AS AssigneeUserId,
       COALESCE(
               NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(u.FirstName, N''), N' ', NULLIF(u.LastName, N'')))), N''),
               roster.MemberUsername)                                                      AS MemberName,
       roster.MemberRole,
       roster.VoteStatus,
       CASE
           WHEN roster.VoteStatus = 'Pending' THEN 'Pending'
           WHEN roster.VoteStatus IN ('approve', 'Approve') THEN 'Agree'
           WHEN roster.VoteStatus IN ('reject', 'Reject') THEN 'Disagree'
           WHEN roster.VoteStatus IN ('route_back', 'RouteBack') THEN 'Route Back'
           ELSE roster.VoteStatus
       END                                                                                 AS VoteLabel,
       CAST(CASE WHEN roster.VoteStatus = 'Pending' THEN 1 ELSE 0 END AS BIT)              AS IsPending,
       roster.VotedAt,
       roster.Comments,
       roster.DueAt,
       roster.SlaStatus
FROM roster
         JOIN workflow.WorkflowInstances wi ON wi.Id = roster.WorkflowInstanceId
         JOIN appraisal.Appraisals a ON a.RequestId = TRY_CAST(wi.CorrelationId AS UNIQUEIDENTIFIER)
         LEFT JOIN auth.AspNetUsers u ON u.UserName = roster.MemberUsername
         -- Committee-approval outcome row (post-approval): linked meeting.
         OUTER APPLY (SELECT TOP 1 ar.MeetingId
                      FROM appraisal.AppraisalReviews ar
                      WHERE ar.AppraisalId = a.Id) rv
         LEFT JOIN workflow.Meetings rvm ON rvm.Id = rv.MeetingId
         -- In-flight committee code stamped on the appraisal's pending-approval tasks.
         OUTER APPLY (SELECT TOP 1 pt2.CommitteeCode
                      FROM workflow.PendingTasks pt2
                      WHERE pt2.WorkflowInstanceId = roster.WorkflowInstanceId
                        AND pt2.ActivityId = 'pending-approval'
                        AND pt2.CommitteeCode IS NOT NULL
                      ORDER BY pt2.AssignedAt DESC) ptc;
