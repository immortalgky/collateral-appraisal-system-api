using Dapper;
using Shared.Data;
using Shared.Time;
using Workflow.Contracts.Sql;
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
    string? AgendaOthers,
    DateTime? CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

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
    DateTime? CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    string? PreviousEndedMeetingNo,
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
    decimal? AppraisedValue)
{
    /// <summary>
    /// Individual approver votes for this appraisal's CURRENT approval round only, oldest first.
    ///
    /// A recall does not delete votes — it completes the approval round and leaves its rows in
    /// <c>workflow.ApprovalVotes</c> — and the same member may vote again in the next round (the
    /// unique index is on <c>(ActivityExecutionId, Member)</c>). Returning votes across rounds
    /// would therefore list the same person twice, so this is scoped to the newest
    /// <c>pending-approval</c> execution and suppressed unless the item is currently Released.
    ///
    /// Empty for items that are Pending or RoutedBack, for Acknowledgement items (no workflow
    /// gate), and for a re-released item nobody has voted on yet.
    ///
    /// Returned per voter rather than aggregated so the UI can name who voted and, by
    /// subtracting from the meeting roster, who still owes a vote.
    /// </summary>
    public List<ItemVoteDto> Votes { get; init; } = [];

    /// <summary>Timestamp of the most recent approver vote, or null when none has been cast.</summary>
    public DateTime? LastVotedAt { get; init; }
}

/// <summary>
/// One approver's vote. <paramref name="Member"/> is the username, matching
/// <c>MeetingMemberDto.UserId</c> so the UI can resolve it to a display name.
/// <paramref name="Vote"/> is workflow-config driven (<c>voteOptions</c>), not a fixed enum.
/// </summary>
public record ItemVoteDto(string Member, string? MemberRole, string Vote, DateTime VotedAt);

/// <summary>Stable grouped shape with typed arrays — TypeScript-friendly, no dynamic dictionary keys.</summary>
public record MeetingItemGroupDto(string Group, List<MeetingItemDto> Items);

/// <summary>
/// Decision items in one entry per AppraisalType group; Acknowledgement items in one entry per group.
/// Empty groups are omitted.
/// </summary>
public record MeetingItemsGroupedDto(
    List<MeetingItemGroupDto> DecisionItems,
    List<MeetingItemGroupDto> AcknowledgementItems);

/// <summary>Raw vote row read from SQL before being folded into <see cref="MeetingItemDto"/>.</summary>
internal record VoteRow(Guid AppraisalId, string Member, string? MemberRole, string Vote, DateTime VotedAt);

public class GetMeetingDetailQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetMeetingDetailQuery, MeetingDetailDto>
{
    /// <summary>
    /// The workflow activity that runs a committee approval round. Same identifier the recall
    /// guard (<c>RecallMeetingItemEndpoint</c>) and <c>GetApprovalHistoryQuery</c> use.
    /// </summary>
    private const string ApprovalActivityId = "pending-approval";

    public async Task<MeetingDetailDto> Handle(GetMeetingDetailQuery query, CancellationToken cancellationToken)
    {
        var sql = """
                           SELECT
                               Id, Title, Status, MeetingNo, StartAt, EndAt,
                               Location, Notes, CancelReason, EndedAt, CancelledAt, CutOffAt,
                               InvitationSentAt, FromText, ToText,
                               AgendaCertifyMinutes, AgendaChairmanInformed, AgendaOthers,
                               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                           FROM workflow.[Meetings] m WHERE m.[Id] = @Id

                           SELECT
                               Id, UserId, MemberName, Position, SourceCommitteeMemberId, AddedAt
                           FROM workflow.[MeetingMembers] where MeetingId = @Id

                           SELECT
                               m.Id, a.Id AS AppraisalId, a.AppraisalNumber, a.FacilityLimit, WorkflowInstanceId,
                               ActivityId, Kind, a.AppraisalType, AcknowledgementGroup,
                               ItemDecision, DecisionAt, DecisionBy, DecisionReason, AddedAt,
                               c.Name AS CustomerName, u.FirstName + ' ' + u.LastName AS AppraisalStaff,
                               v.AppraisedValue
                           FROM workflow.MeetingItems m
                               INNER JOIN appraisal.Appraisals a ON a.Id = m.AppraisalId
                               CROSS APPLY (SELECT TOP 1 Name
                                            FROM request.RequestCustomers
                                            WHERE RequestId = a.RequestId) c
                               OUTER APPLY (SELECT TOP 1 Id, COALESCE(AssigneeUserId, InternalAppraiserId) AS InternalAppraiserId
                                            FROM appraisal.AppraisalAssignments
                                            WHERE AppraisalId = a.Id AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                                            ORDER BY AssignedAt DESC, CreatedAt DESC) AA
                               LEFT JOIN auth.AspNetUsers u ON u.UserName = aa.InternalAppraiserId
                               LEFT JOIN appraisal.ValuationAnalyses v ON v.AppraisalId = a.Id
                               WHERE MeetingId = @Id

                           """ + "\n\n" + PreviousMeetingNoQuery.Sql("Id") + "\n\n" + """

                           -- Votes are scoped to the CURRENT approval round. A recall completes the round
                           -- but leaves its votes behind, and the same member may vote again next round
                           -- (unique index is on (ActivityExecutionId, Member)) — so joining on appraisal
                           -- alone double-counts people. Ordering by StartedOn (Id only as a deterministic
                           -- tiebreaker) rather than by latest vote is deliberate: a re-released item that
                           -- nobody has voted on yet must report nothing, not fall back to the dead round.
                           SELECT mi.AppraisalId, v.Member, v.MemberRole, v.Vote, v.VotedAt
                           FROM workflow.MeetingItems mi
                               CROSS APPLY (SELECT TOP 1 ae.Id
                                            FROM workflow.WorkflowActivityExecutions ae
                                            WHERE ae.WorkflowInstanceId = mi.WorkflowInstanceId
                                              AND ae.ActivityId = @ApprovalActivityId
                                            ORDER BY ae.StartedOn DESC, ae.Id DESC) latest
                               INNER JOIN workflow.ApprovalVotes v
                                   ON v.ActivityExecutionId = latest.Id
                                   AND v.AppraisalId = mi.AppraisalId
                           WHERE mi.MeetingId = @Id
                             AND mi.ItemDecision = 'Released'
                           ORDER BY v.VotedAt
                           """;

        var connection = sqlConnectionFactory.GetOpenConnection();
        await using var multi = await connection.QueryMultipleAsync(sql, new { query.Id, ApprovalActivityId });

        var meeting = await multi.ReadSingleAsync<MeetingDto>();

        var members = (await multi.ReadAsync<MeetingMemberDto>()).ToList();

        var allItems = (await multi.ReadAsync<MeetingItemDto>()).ToList();

        var previousEndedMeetingNo = await multi.ReadFirstOrDefaultAsync<string?>();

        var voteRows = (await multi.ReadAsync<VoteRow>()).ToList();

        // Attach the approver votes to each item. Items with no votes keep the empty default,
        // so the UI can distinguish "not yet released" from "released, nobody voted".
        if (voteRows.Count > 0)
        {
            var votesByAppraisal = voteRows
                .GroupBy(v => v.AppraisalId)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Votes: g
                            .OrderBy(v => v.VotedAt)
                            .Select(v => new ItemVoteDto(v.Member, v.MemberRole, v.Vote, v.VotedAt))
                            .ToList(),
                        LastVotedAt: (DateTime?)g.Max(v => v.VotedAt)));

            for (var i = 0; i < allItems.Count; i++)
            {
                if (!votesByAppraisal.TryGetValue(allItems[i].AppraisalId, out var voted))
                    continue;

                allItems[i] = allItems[i] with
                {
                    Votes = voted.Votes,
                    LastVotedAt = voted.LastVotedAt
                };
            }
        }

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
                              && meeting.StartAt.Value <= dateTimeProvider.ApplicationNow
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
            meeting.CreatedAt,
            meeting.CreatedBy,
            meeting.UpdatedAt,
            meeting.UpdatedBy,
            previousEndedMeetingNo,
            members,
            grouped);
    }
}