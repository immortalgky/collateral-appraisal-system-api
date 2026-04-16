CREATE OR ALTER VIEW workflow.vw_MeetingRoster
AS
SELECT
    m.Id          AS MeetingId,
    m.MeetingNo,
    m.Title       AS MeetingTitle,
    m.Status      AS MeetingStatus,
    mm.Id         AS MeetingMemberId,
    mm.UserId,
    mm.MemberName,
    mm.Position,
    mm.SourceCommitteeMemberId,
    mm.AddedAt
FROM workflow.Meetings m
         INNER JOIN workflow.MeetingMembers mm
                    ON mm.MeetingId = m.Id
