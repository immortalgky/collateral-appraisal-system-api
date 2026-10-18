-- Backfill workflow.Meetings.CommitteeId for meetings created before the column existed.
--
-- A released meeting item is now approved under the meeting's own committee (Meeting.CommitteeId)
-- rather than the tier its current appraisal value would pick, so an appraisal routed back and
-- reworked below the meeting threshold is still judged by the group that heard it. The release
-- gate refuses to release an item from a meeting left NULL, so this must run before go-live.
--
-- CreateMeeting always snapshots COMMITTEE_WITH_MEETING, but BulkCreateMeetings accepts an optional
-- CommitteeId, so a meeting's committee is not always that one. Step 1 recovers it from the roster
-- snapshot (MeetingMembers.SourceCommitteeMemberId -> CommitteeMembers.CommitteeId); step 2 falls
-- back to COMMITTEE_WITH_MEETING for meetings whose snapshot members are all manual or deleted.
-- Only NULL rows are touched, so the script is idempotent.

UPDATE m
SET m.[CommitteeId] = src.[CommitteeId]
FROM [workflow].[Meetings] m
CROSS APPLY (
    SELECT TOP (1) cm.[CommitteeId]
    FROM [workflow].[MeetingMembers] mm
    JOIN [workflow].[CommitteeMembers] cm ON cm.[Id] = mm.[SourceCommitteeMemberId]
    WHERE mm.[MeetingId] = m.[Id]
    GROUP BY cm.[CommitteeId]
    ORDER BY COUNT(*) DESC, cm.[CommitteeId]   -- tie-break so reruns pick the same committee
) src
WHERE m.[CommitteeId] IS NULL;

UPDATE m
SET m.[CommitteeId] = c.[Id]
FROM [workflow].[Meetings] m
CROSS JOIN [workflow].[Committees] c
WHERE c.[Code] = N'COMMITTEE_WITH_MEETING'
  AND m.[CommitteeId] IS NULL;
