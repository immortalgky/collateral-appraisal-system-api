using Dapper;
using Shared.Data;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.GetMeetingDetail;

public class GetMeetingDetailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/meetings/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new GetMeetingDetailQuery(id), ct);
                return Results.Ok(result);
            })
            .WithName("GetMeetingDetail")
            .WithTags("Meetings")
            .RequireAuthorization("CommitteeMember");
    }
}

public record GetMeetingDetailQuery(Guid Id) : IQuery<MeetingDetailDto>;

public record MeetingDto(
    Guid Id,
    string Title,
    string Status,
    string? MeetingNo,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location,
    string? Notes,
    string? CancelReason,
    DateTime? EndedAt,
    DateTime? CancelledAt,
    DateTime? CutOffAt,
    DateTime? InvitationSentAt,
    string? FromText,
    string? ToText,
    string? AgendaCertifyMinutes,
    string? AgendaChairmanInformed,
    string? AgendaOthers);

public record MeetingDetailDto(
    Guid Id,
    string Title,
    string Status,
    string? MeetingNo,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location,
    string? Notes,
    string? CancelReason,
    DateTime? EndedAt,
    DateTime? CancelledAt,
    DateTime? CutOffAt,
    DateTime? InvitationSentAt,
    string? FromText,
    string? ToText,
    string? AgendaCertifyMinutes,
    string? AgendaChairmanInformed,
    string? AgendaOthers,
    List<MeetingMemberDto> Members,
    MeetingItemsGroupedDto Items);

public record MeetingMemberDto(
    Guid Id,
    string UserId,
    string MemberName,
    string Position,
    Guid? SourceCommitteeMemberId,
    DateTime AddedAt);

public record MeetingItemDto(
    Guid Id,
    Guid AppraisalId,
    string? AppraisalNumber,
    decimal FacilityLimit,
    Guid? WorkflowInstanceId,
    string? ActivityId,
    string Kind,
    string? AppraisalType,
    string? AcknowledgementGroup,
    string ItemDecision,
    DateTime? DecisionAt,
    string? DecisionBy,
    string? DecisionReason,
    DateTime AddedAt,
    string CustomerName,
    string AppraisalStaff,
    decimal? AppraisedValue);

/// <summary>Stable grouped shape with typed arrays — TypeScript-friendly, no dynamic dictionary keys.</summary>
public record MeetingItemGroupDto(string Group, List<MeetingItemDto> Items);

/// <summary>
/// Decision items in one entry per AppraisalType group; Acknowledgement items in one entry per group.
/// Empty groups are omitted.
/// </summary>
public record MeetingItemsGroupedDto(
    List<MeetingItemGroupDto> DecisionItems,
    List<MeetingItemGroupDto> AcknowledgementItems);

public class GetMeetingDetailQueryHandler(
    IMeetingRepository meetingRepository,
    ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetMeetingDetailQuery, MeetingDetailDto>
{
    public async Task<MeetingDetailDto> Handle(GetMeetingDetailQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT 
                               Id, Title, Status, MeetingNo, StartAt, EndAt, 
                               Location, Notes, CancelReason, EndedAt, CancelledAt, CutOffAt, 
                               InvitationSentAt, FromText, ToText, 
                               AgendaCertifyMinutes, AgendaChairmanInformed, AgendaOthers
                           FROM workflow.[Meetings] m WHERE m.[Id] = @Id

                           SELECT 
                               Id, UserId, MemberName, Position, SourceCommitteeMemberId, AddedAt
                           FROM workflow.[MeetingMembers] where MeetingId = @Id

                           SELECT
                               m.Id, a.Id AS AppraisalId, a.AppraisalNumber, a.FacilityLimit, WorkflowInstanceId,
                               ActivityId, Kind, a.AppraisalType, AcknowledgementGroup,
                               ItemDecision, DecisionAt, DecisionBy, DecisionReason, AddedAt,
                               c.Name AS CustomerName, u.FirstName + ' ' + u.LastName AS AppraisalStaff, v.AppraisedValue
                           FROM workflow.MeetingItems m
                               INNER JOIN appraisal.Appraisals a ON a.Id = m.AppraisalId
                               CROSS APPLY (SELECT TOP 1 Name
                                            FROM request.RequestCustomers
                                            WHERE RequestId = a.RequestId) c
                               OUTER APPLY (SELECT TOP 1 Id, COALESCE(AssigneeUserId, InternalAppraiserId) AS InternalAppraiserId
                                            FROM appraisal.AppraisalAssignments
                                            WHERE AppraisalId = a.Id AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                                            ORDER BY AssignedAt DESC) AA
                               LEFT JOIN auth.AspNetUsers u ON u.UserName = aa.InternalAppraiserId
                               LEFT JOIN appraisal.ValuationAnalyses v ON v.AppraisalId = a.Id
                               WHERE MeetingId = @Id
                           """;

        var connection = sqlConnectionFactory.GetOpenConnection();
        await using var multi = await connection.QueryMultipleAsync(sql, new { query.Id });

        var meeting = await multi.ReadSingleAsync<MeetingDto>();

        var members = (await multi.ReadAsync<MeetingMemberDto>()).ToList();

        var allItems = (await multi.ReadAsync<MeetingItemDto>()).ToList();

        // Decision items: one group per AppraisalType, known types first then any others.
        var decisionItemsList = allItems
            .Where(i => i.Kind == MeetingItemKind.Decision.ToString())
            .ToList();

        var decisionGroups = decisionItemsList
            .GroupBy(i => i.AppraisalType ?? "Other")
            .Select(g => new MeetingItemGroupDto(g.Key, g.ToList()))
            .ToList();

        // Acknowledgement items: one group per AcknowledgementGroup key.
        var ackItemsList = allItems
            .Where(i => i.Kind == MeetingItemKind.Acknowledgement.ToString())
            .ToList();

        var ackGroups = ackItemsList
            .GroupBy(i => i.AcknowledgementGroup ?? "Unknown")
            .Select(g => new MeetingItemGroupDto(g.Key, g.ToList()))
            .ToList();

        var grouped = new MeetingItemsGroupedDto(decisionGroups, ackGroups);

        // Compute effective status: InvitationSent + now >= StartAt → InProgress (never persisted).
        var effectiveStatus = meeting.Status == nameof(MeetingStatus.InvitationSent)
                              && meeting.StartAt.HasValue
                              && meeting.StartAt.Value <= DateTime.UtcNow
            ? "InProgress"
            : meeting.Status;

        return new MeetingDetailDto(
            meeting.Id,
            meeting.Title,
            effectiveStatus,
            meeting.MeetingNo,
            meeting.StartAt,
            meeting.EndAt,
            meeting.Location,
            meeting.Notes,
            meeting.CancelReason,
            meeting.EndedAt,
            meeting.CancelledAt,
            meeting.CutOffAt,
            meeting.InvitationSentAt,
            meeting.FromText,
            meeting.ToText,
            meeting.AgendaCertifyMinutes,
            meeting.AgendaChairmanInformed,
            meeting.AgendaOthers,
            members,
            grouped);
    }
}