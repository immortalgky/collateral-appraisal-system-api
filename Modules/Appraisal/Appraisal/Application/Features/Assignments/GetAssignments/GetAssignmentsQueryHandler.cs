using Auth.Contracts.Users;
using Microsoft.EntityFrameworkCore;

namespace Appraisal.Application.Features.Assignments.GetAssignments;

public class GetAssignmentsQueryHandler(
    AppraisalDbContext dbContext,
    IUserLookupService userLookup,
    ILogger<GetAssignmentsQueryHandler> logger)
    : IQueryHandler<GetAssignmentsQuery, GetAssignmentsResult>
{
    /// <summary>Time budget of the best-effort name lookup; past it the names come back null.</summary>
    internal TimeSpan LookupTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public async Task<GetAssignmentsResult> Handle(
        GetAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.AppraisalAssignments
            .AsNoTracking()
            .Where(a => a.AppraisalId == query.AppraisalId)
            .Include(a => a.Cycles)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // Filter after materialization: AssignmentStatus is a HasConversion value object and
        // EF cannot translate value-object equality to SQL. Row count per appraisal is tiny.
        var assignments = rows
            .Where(a => a.AssignmentStatus != AssignmentStatus.Rejected
                        && a.AssignmentStatus != AssignmentStatus.Cancelled)
            .ToList();

        // Only an offline engagement stores a hand-keyed date; on every other path ValuationDate is
        // derived from the appointment and must not be presented as a keyed book date. Fetched once
        // and only when an offline row is present.
        DateTime? offlineBookDate = null;
        if (assignments.Any(a => a.IsOfflineEngagement))
        {
            offlineBookDate = await dbContext.ValuationAnalyses
                .AsNoTracking()
                .Where(v => v.AppraisalId == query.AppraisalId)
                .Select(v => (DateTime?)v.ValuationDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // One batched lookup for every employee code the rows carry (assigner, executor, follow-up staff). Best effort:
        // a display name must never fail the endpoint, so a lookup failure logs and the names come back null (the
        // codes are still in the DTO). A caller abort is not a lookup failure and still stops the request.
        var codes = assignments
            .SelectMany(a => new[] { a.AssignedBy, a.AssigneeUserId, a.InternalAppraiserId })
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyDictionary<string, UserLookupDto> people = new Dictionary<string, UserLookupDto>();
        using var lookupBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lookupBudget.CancelAfter(LookupTimeout);
        try
        {
            people = await userLookup.GetByUsernamesAsync(codes, lookupBudget.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Includes the budget running out (the linked token cancels the lookup, the caller's does not).
            logger.LogWarning(ex,
                "Display-name lookup failed or timed out for the assignments of appraisal {AppraisalId}; returning codes without names",
                query.AppraisalId);
        }

        var dtos = assignments.Select(a => new AssignmentDto(
            a.Id,
            a.AppraisalId,
            a.AssignmentType.Code,
            a.AssignmentStatus.Code,
            a.AssigneeUserId,
            a.AssigneeCompanyId,
            a.InternalAppraiserId,
            a.InternalFollowupAssignmentMethod,
            a.AssignmentMethod,
            a.ReassignmentNumber,
            a.ProgressPercent,
            a.AssignedAt,
            a.AssignedBy,
            a.StartedAt,
            a.SubmittedAt,
            a.CompletedAt,
            a.RejectionReason,
            a.CancellationReason,
            a.Remark,
            a.DraftSavedAt,
            a.CreatedAt,
            a.Cycles.OrderBy(c => c.CycleNumber).Select(c => new EngagementCycleDto(
                c.Id,
                c.CycleNumber,
                c.OpenedAt,
                c.ClosedAt,
                c.BusinessMinutes,
                c.Status)).ToList(),
            a.TotalExternalBusinessMinutes,
            a.SubmissionCount,
            a.IsOfflineEngagement ? offlineBookDate : null,
            PersonName(people, a.AssignedBy),
            PersonName(people, a.AssigneeUserId),
            PersonName(people, a.InternalAppraiserId)
        )).ToList();

        return new GetAssignmentsResult(dtos);
    }

    private static string? PersonName(IReadOnlyDictionary<string, UserLookupDto> users, string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || !users.TryGetValue(code, out var u)) return null;
        var name = $"{u.FirstName} {u.LastName}".Trim();
        return name.Length == 0 ? null : name;
    }
}
