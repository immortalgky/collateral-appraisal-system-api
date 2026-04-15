using Workflow.Domain.Committees;
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
    string? AppraisalNo,
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
    DateTime AddedAt);

/// <summary>Stable grouped shape with typed arrays — TypeScript-friendly, no dynamic dictionary keys.</summary>
public record MeetingItemGroupDto(string Group, List<MeetingItemDto> Items);

/// <summary>
/// Decision items in one entry per AppraisalType group; Acknowledgement items in one entry per group.
/// Empty groups are omitted.
/// </summary>
public record MeetingItemsGroupedDto(
    List<MeetingItemGroupDto> DecisionItems,
    List<MeetingItemGroupDto> AcknowledgementItems);

public class GetMeetingDetailQueryHandler(IMeetingRepository meetingRepository)
    : IQueryHandler<GetMeetingDetailQuery, MeetingDetailDto>
{
    public async Task<MeetingDetailDto> Handle(GetMeetingDetailQuery query, CancellationToken cancellationToken)
    {
        var meeting = await meetingRepository.GetByIdForDecisionAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Meeting {query.Id} not found");

        var members = meeting.Members
            .Select(m => new MeetingMemberDto(
                m.Id, m.UserId, m.MemberName, m.Position.ToString(),
                m.SourceCommitteeMemberId, m.AddedAt))
            .ToList();

        var allItemDtos = meeting.Items
            .Select(i => new MeetingItemDto(
                i.Id, i.AppraisalId, i.AppraisalNo, i.FacilityLimit,
                i.WorkflowInstanceId, i.ActivityId,
                i.Kind.ToString(), i.AppraisalType, i.AcknowledgementGroup,
                i.ItemDecision.ToString(), i.DecisionAt, i.DecisionBy, i.DecisionReason,
                i.AddedAt))
            .ToList();

        // Decision items: one group per AppraisalType, known types first then any others.
        var decisionItemsList = allItemDtos
            .Where(i => i.Kind == MeetingItemKind.Decision.ToString())
            .ToList();

        var decisionGroups = decisionItemsList
            .GroupBy(i => i.AppraisalType ?? "Other")
            .Select(g => new MeetingItemGroupDto(g.Key, g.ToList()))
            .ToList();

        // Acknowledgement items: one group per AcknowledgementGroup key.
        var ackItemsList = allItemDtos
            .Where(i => i.Kind == MeetingItemKind.Acknowledgement.ToString())
            .ToList();

        var ackGroups = ackItemsList
            .GroupBy(i => i.AcknowledgementGroup ?? "Unknown")
            .Select(g => new MeetingItemGroupDto(g.Key, g.ToList()))
            .ToList();

        var grouped = new MeetingItemsGroupedDto(decisionGroups, ackGroups);

        return new MeetingDetailDto(
            meeting.Id,
            meeting.Title,
            meeting.Status.ToString(),
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
