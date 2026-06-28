using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles a <see cref="MeetingMinuteModel"/> for the "Meeting Minute"
/// PDF report (รายงานการประชุมคณะกรรมการ).  FSD §2.1.9.
///
/// Data strategy (READ-ONLY Dapper — no EF, no migrations):
///   Same meeting/member/item queries as MeetingInvitationDataProvider.
///   Additionally joins appraisal.CommitteeVotes (via AppraisalReviews) to
///   pull per-member vote/opinion for the signature block.
///
/// Deferred fields (no current schema source):
///   - Staff presenters table (เจ้าหน้าที่นำเสนอ) — rendered as empty placeholder.
///
/// entityId = MeetingId (Guid).
/// </summary>
public sealed class MeetingMinuteDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<MeetingMinuteDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "meeting-minute";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var meetingId))
            throw new NotFoundException("Meeting", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var p = new DynamicParameters();
        p.Add("MeetingId", meetingId);

        // ── 1. Meeting header ─────────────────────────────────────────────────
        const string headerSql = """
            SELECT
                MeetingNo, StartAt, EndAt, Location,
                AgendaCertifyMinutes, AgendaChairmanInformed, AgendaOthers,
                EndedAt
            FROM workflow.Meetings
            WHERE Id = @MeetingId
            """;

        var header = await connection.QueryFirstOrDefaultAsync<MinuteHeaderRow>(headerSql, p);
        if (header is null)
            throw new NotFoundException("Meeting", entityId);

        // ── 2. Members ────────────────────────────────────────────────────────
        const string membersSql = """
            SELECT MemberName, Position
            FROM workflow.MeetingMembers
            WHERE MeetingId = @MeetingId
            ORDER BY
                CASE Position
                    WHEN 'Chairman'  THEN 1
                    WHEN 'Director'  THEN 2
                    WHEN 'Secretary' THEN 9
                    ELSE 5
                END, MemberName
            """;

        var memberRows = (await connection.QueryAsync<MemberRow>(membersSql, p)).ToList();

        // ── 3. Items with CustomerName + AppraisedValue + IsPriceVerified ─────
        const string itemsSql = """
            SELECT
                mi.AppraisalId,
                mi.Kind,
                a.AppraisalType,
                mi.AcknowledgementGroup,
                mi.FacilityLimit,
                c.Name AS CustomerName,
                pr.ProjectName,
                u.FirstName + ' ' + u.LastName AS AppraisalStaff,
                u.Position AS StaffPosition,
                v.AppraisedValue,
                ad.IsPriceVerified
            FROM workflow.MeetingItems mi
            INNER JOIN appraisal.Appraisals a ON a.Id = mi.AppraisalId
            OUTER APPLY (
                SELECT STRING_AGG(rc.Name, '||') AS Name
                FROM request.RequestCustomers rc
                WHERE rc.RequestId = a.RequestId
            ) c
            OUTER APPLY (
                SELECT TOP 1 COALESCE(AssigneeUserId, InternalAppraiserId) AS InternalAppraiserId
                FROM appraisal.AppraisalAssignments
                WHERE AppraisalId = a.Id AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                ORDER BY AssignedAt DESC, CreatedAt DESC
            ) aa
            LEFT JOIN auth.AspNetUsers u ON u.UserName = aa.InternalAppraiserId
            LEFT JOIN appraisal.Projects pr ON pr.AppraisalId = a.Id
            LEFT JOIN appraisal.ValuationAnalyses v ON v.AppraisalId = a.Id
            LEFT JOIN appraisal.AppraisalDecisions ad ON ad.AppraisalId = a.Id
            WHERE mi.MeetingId = @MeetingId
            """;

        var items = (await connection.QueryAsync<MeetingItemFlat>(itemsSql, p)).ToList();

        // ── 4. Previous ended meeting number ──────────────────────────────────
        const string prevSql = """
            SELECT TOP 1 prev.MeetingNo
            FROM workflow.Meetings curr
            INNER JOIN workflow.Meetings prev
                ON prev.Status = 'Ended'
                AND prev.Id <> curr.Id
                AND prev.EndedAt IS NOT NULL
            WHERE curr.Id = @MeetingId
              AND (curr.StartAt IS NULL OR prev.EndedAt < curr.StartAt)
            ORDER BY prev.EndedAt DESC
            """;

        var previousMeetingNo = await connection.QueryFirstOrDefaultAsync<string?>(prevSql, p);

        // ── 5. Committee votes per member (best-effort consensus opinion). ──────
        //       Join via meeting items → AppraisalReviews → CommitteeVotes, matched
        //       back to MeetingMembers by snapshot MemberName. LIMITATION: there is no
        //       stable id linking workflow.MeetingMembers to appraisal.CommitteeMembers,
        //       so this matches on name only. The inner subquery is GROUPed to exactly
        //       one row per MemberName (deterministic via MIN) so the outer LEFT JOIN can
        //       never multiply a member into duplicate signature rows even when that
        //       member voted on several appraisals.
        const string votesSql = """
            SELECT
                mm.MemberName,
                mm.Position,
                cv.Vote,
                cv.Opinion
            FROM workflow.MeetingMembers mm
            LEFT JOIN (
                SELECT
                    cm.MemberName,
                    MIN(cv.Vote)     AS Vote,
                    MIN(cv.Comments) AS Opinion
                FROM workflow.MeetingItems mi
                INNER JOIN appraisal.AppraisalReviews ar ON ar.AppraisalId = mi.AppraisalId
                INNER JOIN appraisal.CommitteeVotes cv ON cv.ReviewId = ar.Id
                INNER JOIN appraisal.CommitteeMembers cm ON cm.Id = cv.CommitteeMemberId
                WHERE mi.MeetingId = @MeetingId
                GROUP BY cm.MemberName
            ) cv ON cv.MemberName = mm.MemberName
            WHERE mm.MeetingId = @MeetingId
            ORDER BY
                CASE mm.Position
                    WHEN 'Chairman'  THEN 1
                    WHEN 'Director'  THEN 2
                    WHEN 'Secretary' THEN 9
                    ELSE 5
                END, mm.MemberName
            """;

        var voteRows = (await connection.QueryAsync<VoteRow>(votesSql, p)).ToList();

        // ── Build model ───────────────────────────────────────────────────────
        var members = memberRows.Select(m => new MeetingMemberRow
        {
            MemberName = m.MemberName,
            PositionThai = MeetingInvitationDataProvider.MapPositionThai(m.Position)
        }).ToList();

        var agendas = MeetingAgendaBuilder.Build(
            items,
            previousMeetingNo,
            header.AgendaCertifyMinutes,
            header.AgendaChairmanInformed,
            header.AgendaOthers);

        var presenters = MeetingAgendaBuilder.BuildPresenters(items);

        var committeeOpinions = voteRows.Select(v => new CommitteeOpinionRow
        {
            MemberName = v.MemberName,
            PositionThai = MeetingInvitationDataProvider.MapPositionThai(v.Position),
            VoteLabel = MapVoteLabel(v.Vote),
            Opinion = v.Opinion ?? string.Empty
        }).ToList();

        // Use EndedAt as the minute date (most accurate), fallback to StartAt
        var minuteDate = header.EndedAt ?? header.StartAt;

        var model = new MeetingMinuteModel
        {
            MeetingNo = header.MeetingNo,
            StartAt = header.StartAt,
            EndAt = header.EndAt,
            Location = header.Location,
            MinuteDate = minuteDate,
            Members = members,
            Presenters = presenters,
            Agendas = agendas,
            CommitteeOpinions = committeeOpinions
        };

        logger.LogDebug(
            "MeetingMinute model assembled for meeting {MeetingId}: {AgendaCount} agendas, {MemberCount} members",
            meetingId, agendas.Count, members.Count);

        return model;
    }

    private static string MapVoteLabel(string? vote) => vote switch
    {
        "Approve"    => "เห็นด้วย",
        "Reject"     => "ไม่เห็นด้วย",
        "RouteBack"  => "ส่งกลับ",
        null or ""   => string.Empty,
        _            => vote
    };

    // ── Private Dapper flat DTOs ──────────────────────────────────────────────

    private sealed class MinuteHeaderRow
    {
        public string? MeetingNo { get; init; }
        public DateTime? StartAt { get; init; }
        public DateTime? EndAt { get; init; }
        public string? Location { get; init; }
        public DateTime? EndedAt { get; init; }
        public string? AgendaCertifyMinutes { get; init; }
        public string? AgendaChairmanInformed { get; init; }
        public string? AgendaOthers { get; init; }
    }

    private sealed class MemberRow
    {
        public string MemberName { get; init; } = string.Empty;
        public string Position { get; init; } = string.Empty;
    }

    private sealed class VoteRow
    {
        public string MemberName { get; init; } = string.Empty;
        public string Position { get; init; } = string.Empty;
        public string? Vote { get; init; }
        public string? Opinion { get; init; }
    }
}
