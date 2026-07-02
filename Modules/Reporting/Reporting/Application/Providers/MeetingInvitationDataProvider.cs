using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles a <see cref="MeetingInvitationModel"/> for the "Meeting Invitation"
/// PDF report (บันทึกภายใน / เชิญประชุมคณะกรรมการ).  FSD §2.1.8.
///
/// Data strategy (READ-ONLY Dapper — no EF, no migrations):
///   workflow.Meetings              → header fields
///   workflow.MeetingMembers        → committee member roster
///   workflow.MeetingItems          → decision items (Kind, AppraisalType, FacilityLimit)
///   appraisal.Appraisals           → AppraisalType (authoritative), RequestId
///   request.RequestCustomers       → CustomerName (first row per RequestId)
///   appraisal.ValuationAnalyses    → AppraisedValue
///   appraisal.AppraisalDecisions   → IsPriceVerified (false → "ไม่รับรองราคา")
///   previous-ended-meeting sub-query identical to GetMeetingDetailQueryHandler
///
/// entityId = MeetingId (Guid).
/// </summary>
public sealed class MeetingInvitationDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<MeetingInvitationDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "meeting-invitation";

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
                FromText, ToText, InvitationSentAt,
                AgendaCertifyMinutes, AgendaChairmanInformed, AgendaOthers
            FROM workflow.Meetings
            WHERE Id = @MeetingId
            """;

        var header = await connection.QueryFirstOrDefaultAsync<MeetingHeaderRow>(headerSql, p);
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
                v.AppraisedValue,
                ad.IsPriceVerified
            FROM workflow.MeetingItems mi
            INNER JOIN appraisal.Appraisals a ON a.Id = mi.AppraisalId
            OUTER APPLY (
                SELECT STRING_AGG(rc.Name, '||') AS Name
                FROM request.RequestCustomers rc
                WHERE rc.RequestId = a.RequestId
            ) c
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

        // ── Build model ───────────────────────────────────────────────────────
        var members = memberRows.Select(m => new MeetingMemberRow
        {
            MemberName = m.MemberName,
            PositionThai = MapPositionThai(m.Position)
        }).ToList();

        var secretary = memberRows.FirstOrDefault(m =>
            string.Equals(m.Position, "Secretary", StringComparison.OrdinalIgnoreCase));

        var agendas = MeetingAgendaBuilder.Build(
            items,
            previousMeetingNo,
            header.AgendaCertifyMinutes,
            header.AgendaChairmanInformed,
            header.AgendaOthers);

        var model = new MeetingInvitationModel
        {
            MeetingNo = header.MeetingNo,
            StartAt = header.StartAt,
            EndAt = header.EndAt,
            Location = header.Location,
            FromText = header.FromText,
            ToText = header.ToText,
            InvitationDate = header.InvitationSentAt ?? header.StartAt,
            Agendas = agendas,
            Members = members,
            SecretaryName = secretary?.MemberName
        };

        logger.LogDebug(
            "MeetingInvitation model assembled for meeting {MeetingId}: {AgendaCount} agendas, {MemberCount} members",
            meetingId, agendas.Count, members.Count);

        return model;
    }

    // ── Position → Thai label mapping ─────────────────────────────────────────

    internal static string MapPositionThai(string? position) => position switch
    {
        "Chairman"  => "ประธาน",
        "Secretary" => "เลขานุการฯ",
        _           => "กรรมการ"
    };

    // ── Private Dapper flat DTOs ──────────────────────────────────────────────

    private sealed class MeetingHeaderRow
    {
        public string? MeetingNo { get; init; }
        public DateTime? StartAt { get; init; }
        public DateTime? EndAt { get; init; }
        public string? Location { get; init; }
        public string? FromText { get; init; }
        public string? ToText { get; init; }
        public DateTime? InvitationSentAt { get; init; }
        public string? AgendaCertifyMinutes { get; init; }
        public string? AgendaChairmanInformed { get; init; }
        public string? AgendaOthers { get; init; }
    }

    private sealed class MemberRow
    {
        public string MemberName { get; init; } = string.Empty;
        public string Position { get; init; } = string.Empty;
    }
}
