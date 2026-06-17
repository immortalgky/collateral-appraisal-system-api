-- Per-appraisal committee approval OUTCOME, keyed by AppraisalId and fully decoupled from the
-- workflow tables. This is what the approval-history endpoint reads for legacy / migrated
-- appraisals (which have no workflow.WorkflowInstances row): the authoritative approval flag,
-- committee identity, and the linked meeting.
--
-- Based on appraisal.Appraisals so a row always exists for any appraisal. The authoritative
-- "approved by committee" signal is Appraisals.ApprovedByCommittee (the committee code, set only
-- when the committee approved; NULL otherwise) — the caller derives status from it rather than
-- re-counting votes. Committee name and the decision/acknowledgement meeting come from the
-- appraisal.AppraisalReviews outcome row (created at approval) and are best-effort (LEFT JOINs).
CREATE
OR ALTER
VIEW appraisal.vw_AppraisalApprovalOutcome AS
SELECT a.Id AS AppraisalId,
       a.ApprovedByCommittee,
       c.CommitteeCode,
       c.CommitteeName,
       ar.MeetingId,
       m.Title    AS MeetingTitle,
       m.StartAt  AS MeetingStartAt,
       m.EndedAt  AS MeetingEndedAt
FROM appraisal.Appraisals a
LEFT JOIN appraisal.AppraisalReviews ar ON ar.AppraisalId = a.Id
LEFT JOIN appraisal.Committees c ON c.Id = ar.CommitteeId
LEFT JOIN workflow.Meetings m ON m.Id = ar.MeetingId;
