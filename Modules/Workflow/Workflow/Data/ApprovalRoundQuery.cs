using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Workflow.Meetings.Domain;
using Workflow.Workflow.Models;

namespace Workflow.Data;

/// <summary>
/// The one definition of "this Released meeting item's committee approval round has CONCLUDED".
/// The meeting detail read, the EndMeeting guard and ApprovalActivity's end-of-meeting hook all
/// route through <see cref="ConcludedDecision"/> and read the same <see cref="LatestRoundsSql"/>.
///
/// Looks at the LATEST <c>pending-approval</c> execution of the item's workflow instance
/// (<c>StartedOn DESC, Id DESC</c> — same order as GetMeetingDetailEndpoint's vote scope). The round
/// concluded when
/// (a) that execution is Completed with a <c>decision</c> in its OutputData other than
///     <c>recall_to_meeting</c>. A recall is not a conclusion: the item goes back to Pending and the
///     latest row can still be the old recall round until the re-release starts a new one. A Completed
///     round with decision <c>route_back</c> (or none) on an item that is STILL Released means the
///     meeting-side route-back mirror was skipped or failed; the round is over, decision "route_back",
///     so End is not blocked forever; OR
/// (b) it is still open (Pending / InProgress), every voting member of THAT round has voted, and none
///     of the votes is route_back. The engine leaves such a round wedged (only approve advances it),
///     but for the meeting it is over: decision "reject"; OR
/// (c) it is terminal without completing (Cancelled / Failed / Skipped — the workflow was cancelled
///     or the round was cleared out of band): decision "cancelled". Nobody can vote on it any more,
///     so it must not block End forever. Nothing auto-ends the meeting at cancel time; the manual
///     End is the way out.
/// "Latest" (not "any completed") is what keeps a re-released item from counting as concluded because
/// of an older round: its new execution starts open with no votes and wins.
///
/// Member count for (b): the <c>pending_approval_totalMembers</c> workflow-instance variable that
/// ApprovalActivity wrote at round start (the resolved voting roster size — meeting roster minus
/// the secretary, or the committee's members). It is overwritten at each round start, so for the
/// latest open round it is that round's own number. The live MeetingMembers roster is not used.
///
/// Votes for (b) are counted by <c>ActivityExecutionId</c> only, NOT also by the item's AppraisalId:
/// "everyone has voted" is the activity's own <c>allVoted</c> (votes of the execution), and a vote
/// stored with an empty AppraisalId (appraisalId missing from variables) is still a vote in the round.
/// The per-voter list on the detail page keeps the AppraisalId filter because it is for display.
/// </summary>
public static class ApprovalRoundQuery
{
    /// <summary>The workflow activity that runs a committee approval round.</summary>
    public const string ApprovalActivityId = "pending-approval";

    /// <summary>Instance variable holding the round's voting-member count (see class remarks).</summary>
    public const string TotalMembersVariable = "pending_approval_totalMembers";

    /// <summary>
    /// One Released Decision item with its latest approval round. Positional: the column order of
    /// <see cref="LatestRoundsSql"/> MUST match (Dapper binds positional records by order).
    /// </summary>
    public sealed record Round(
        Guid MeetingItemId,
        Guid AppraisalId,
        Guid WorkflowInstanceId,
        string Status,
        string? Decision,
        int Votes,
        int RouteBackVotes,
        int? TotalMembers)
    {
        /// <summary>The concluded decision of this round, or null while it is open.</summary>
        /// <remarks>An unknown status is treated as an open round, never an error.</remarks>
        public string? Outcome => Enum.TryParse<ActivityExecutionStatus>(Status, out var status)
                                  && Enum.IsDefined(status)
            ? ConcludedDecision(status, Decision, Votes, RouteBackVotes, TotalMembers)
            : null;
    }

    /// <summary>
    /// Latest approval round per Released Decision item of a meeting, in the style of
    /// <c>PreviousMeetingNoQuery.Sql</c>: reads one execution row per instance (CROSS APPLY TOP 1),
    /// two vote counts and the member count out of the instance variables — never all past rounds.
    /// <paramref name="meetingIdParam"/> is the SQL parameter holding the meeting Id (without '@').
    /// Items with no approval execution yet produce no row.
    /// </summary>
    public static string LatestRoundsSql(string meetingIdParam) => $"""
        SELECT mi.Id AS MeetingItemId,
               mi.AppraisalId,
               mi.WorkflowInstanceId,
               latest.Status,
               JSON_VALUE(latest.OutputData, '$.decision') AS Decision,
               (SELECT COUNT(*) FROM workflow.ApprovalVotes av
                WHERE av.ActivityExecutionId = latest.Id) AS Votes,
               (SELECT COUNT(*) FROM workflow.ApprovalVotes av
                WHERE av.ActivityExecutionId = latest.Id AND av.Vote = 'route_back') AS RouteBackVotes,
               TRY_CAST(JSON_VALUE(wi.Variables, '$.{TotalMembersVariable}') AS int) AS TotalMembers
        FROM workflow.MeetingItems mi
            LEFT JOIN workflow.WorkflowInstances wi ON wi.Id = mi.WorkflowInstanceId
            CROSS APPLY (SELECT TOP 1 ae.Id, ae.Status, ae.OutputData
                         FROM workflow.WorkflowActivityExecutions ae
                         WHERE ae.WorkflowInstanceId = mi.WorkflowInstanceId
                           AND ae.ActivityId = '{ApprovalActivityId}'
                         ORDER BY ae.StartedOn DESC, ae.Id DESC) latest
        WHERE mi.MeetingId = @{meetingIdParam}
          AND mi.Kind = 'Decision'
          AND mi.ItemDecision = 'Released'
        """;

    /// <summary>
    /// The concluded decision ("approve", "reject", "cancelled", or "route_back" for a Completed round
    /// with that decision or none), or null while the round is open or was recalled
    /// (<c>recall_to_meeting</c>). Raw-value signature so every reader applies the same rule.
    /// </summary>
    public static string? ConcludedDecision(
        ActivityExecutionStatus status, string? decision, int votes, int routeBackVotes, int? totalMembers)
    {
        if (status == ActivityExecutionStatus.Completed)
        {
            if (decision?.Equals(MeetingOutcomes.Recalled, StringComparison.OrdinalIgnoreCase) == true)
                return null;

            return string.IsNullOrWhiteSpace(decision) ? "route_back" : decision;
        }

        if (status is ActivityExecutionStatus.Pending or ActivityExecutionStatus.InProgress)
            return totalMembers is > 0 && votes >= totalMembers && routeBackVotes == 0 ? "reject" : null;

        return "cancelled";
    }

    /// <summary>
    /// For a round that just resolved with <paramref name="decision"/> (the approval hook): true when it
    /// is an approve / reject style conclusion — not a route-back, whose own mirror reopens the meeting.
    /// </summary>
    public static bool IsConcludingDecision(string? decision) =>
        ConcludedDecision(ActivityExecutionStatus.Completed, decision, 0, 0, null) is { } d
        && !d.Equals("route_back", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// MeetingItem Id → concluded decision, for the Released Decision items of
    /// <paramref name="meetingId"/> whose latest round has concluded. Released items absent from the
    /// result are still open.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> ConcludedByItemAsync(
        this WorkflowDbContext db, Guid meetingId, CancellationToken ct)
    {
        var rounds = db.Database.IsRelational()
            ? await db.Database
                .SqlQueryRaw<Round>(LatestRoundsSql("MeetingId"), new SqlParameter("MeetingId", meetingId))
                .ToListAsync(ct)
            : await LatestRoundsWithoutSqlAsync(db, meetingId, ct);

        return rounds.Where(r => r.Outcome is not null).ToDictionary(r => r.MeetingItemId, r => r.Outcome!);
    }

    /// <summary>
    /// Same data as <see cref="LatestRoundsSql"/> through LINQ, for non-relational providers (the
    /// EF InMemory provider the unit tests run on has no raw SQL). Not used against SQL Server.
    /// </summary>
    private static async Task<List<Round>> LatestRoundsWithoutSqlAsync(
        WorkflowDbContext db, Guid meetingId, CancellationToken ct)
    {
        var items = await db.MeetingItems.AsNoTracking()
            .Where(mi => mi.MeetingId == meetingId && mi.Kind == MeetingItemKind.Decision
                         && mi.ItemDecision == ItemDecision.Released && mi.WorkflowInstanceId != null)
            .ToListAsync(ct);

        var rounds = new List<Round>();
        foreach (var item in items)
        {
            var latest = (await db.WorkflowActivityExecutions.AsNoTracking()
                    .Where(ae => ae.WorkflowInstanceId == item.WorkflowInstanceId && ae.ActivityId == ApprovalActivityId)
                    .OrderByDescending(ae => ae.StartedOn).ThenByDescending(ae => ae.Id)
                    .Select(ae => new { ae.Id, ae.Status, ae.OutputData })
                    .Take(1)
                    .ToListAsync(ct))
                .FirstOrDefault();
            if (latest is null) continue;

            var votes = await db.ApprovalVotes.AsNoTracking()
                .Where(v => v.ActivityExecutionId == latest.Id)
                .Select(v => v.Vote)
                .ToListAsync(ct);
            var variables = await db.WorkflowInstances.AsNoTracking()
                .Where(i => i.Id == item.WorkflowInstanceId)
                .Select(i => i.Variables)
                .FirstOrDefaultAsync(ct);

            rounds.Add(new Round(
                item.Id, item.AppraisalId, item.WorkflowInstanceId!.Value, latest.Status.ToString(),
                latest.OutputData.TryGetValue("decision", out var decision) ? decision?.ToString() : null,
                votes.Count, votes.Count(v => v.Equals("route_back", StringComparison.OrdinalIgnoreCase)),
                variables is not null && variables.TryGetValue(TotalMembersVariable, out var total)
                                      && int.TryParse(total?.ToString(), out var n)
                    ? n
                    : null));
        }

        return rounds;
    }
}
