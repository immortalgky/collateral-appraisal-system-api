using Auth.Contracts.Users;
using Shared.Identity;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Application.Queries;

public record ListDocumentFollowupsQuery(
    Guid? RaisingPendingTaskId,
    string? Status,
    Guid? FollowupWorkflowInstanceId) : IRequest<IReadOnlyList<DocumentFollowupSummaryDto>>;

public class ListDocumentFollowupsQueryHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IUserLookupService userLookupService)
    : IRequestHandler<ListDocumentFollowupsQuery, IReadOnlyList<DocumentFollowupSummaryDto>>
{
    public async Task<IReadOnlyList<DocumentFollowupSummaryDto>> Handle(
        ListDocumentFollowupsQuery request, CancellationToken cancellationToken)
    {
        var actor = currentUser.Username;
        if (string.IsNullOrWhiteSpace(actor))
            throw new UnauthorizedAccessException("User not authenticated");

        var query = dbContext.DocumentFollowups.AsNoTracking().AsQueryable();

        if (request.RaisingPendingTaskId.HasValue)
            query = query.Where(f => f.RaisingPendingTaskId == request.RaisingPendingTaskId.Value);

        if (request.FollowupWorkflowInstanceId.HasValue)
            query = query.Where(f => f.FollowupWorkflowInstanceId == request.FollowupWorkflowInstanceId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<DocumentFollowupStatus>(request.Status, true, out var parsed))
            query = query.Where(f => f.Status == parsed);

        var rows = await query.OrderByDescending(f => f.RaisedAt).ToListAsync(cancellationToken);

        // Authorization: caller must be either the raising user (checker) or the assignee of
        // the followup workflow (= the parent workflow's StartedBy, i.e. the request maker).
        // We resolve assignee by loading the followup workflow instances for the rows in scope.
        var followupInstanceIds = rows
            .Where(r => r.FollowupWorkflowInstanceId.HasValue)
            .Select(r => r.FollowupWorkflowInstanceId!.Value)
            .Distinct()
            .ToList();

        var startedByMap = followupInstanceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.WorkflowInstances
                .AsNoTracking()
                .Where(w => followupInstanceIds.Contains(w.Id))
                .Select(w => new { w.Id, w.StartedBy })
                .ToDictionaryAsync(x => x.Id, x => x.StartedBy ?? string.Empty, cancellationToken);

        var visible = rows.Where(f =>
        {
            if (string.Equals(f.RaisingUserId, actor, StringComparison.OrdinalIgnoreCase))
                return true;
            if (f.FollowupWorkflowInstanceId.HasValue &&
                startedByMap.TryGetValue(f.FollowupWorkflowInstanceId.Value, out var startedBy) &&
                string.Equals(startedBy, actor, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }).ToList();

        var distinctUserIds = visible
            .Select(f => f.RaisingUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var userMap = distinctUserIds.Length == 0
            ? (IReadOnlyDictionary<string, UserLookupDto>)new Dictionary<string, UserLookupDto>(StringComparer
                .OrdinalIgnoreCase)
            : await userLookupService.GetByUsernamesAsync(distinctUserIds, cancellationToken);

        return visible.Select(f => GetDocumentFollowupByIdQueryHandler.MapSummary(f, userMap)).ToList();
    }
}