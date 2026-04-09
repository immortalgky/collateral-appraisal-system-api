using Shared.Identity;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Application.Queries;

public record ListDocumentFollowupsQuery(
    Guid? RaisingPendingTaskId,
    string? Status,
    Guid? FollowupWorkflowInstanceId) : IRequest<IReadOnlyList<DocumentFollowupSummaryDto>>;

public class ListDocumentFollowupsQueryHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ListDocumentFollowupsQuery, IReadOnlyList<DocumentFollowupSummaryDto>>
{
    public async Task<IReadOnlyList<DocumentFollowupSummaryDto>> Handle(
        ListDocumentFollowupsQuery request, CancellationToken cancellationToken)
    {
        var actor = currentUser.UserId?.ToString() ?? currentUser.Username;
        if (string.IsNullOrWhiteSpace(actor))
            throw new UnauthorizedAccessException("User not authenticated");

        var query = dbContext.DocumentFollowups.AsNoTracking().AsQueryable();

        if (request.RaisingPendingTaskId.HasValue)
            query = query.Where(f => f.RaisingPendingTaskId == request.RaisingPendingTaskId.Value);

        if (request.FollowupWorkflowInstanceId.HasValue)
            query = query.Where(f => f.FollowupWorkflowInstanceId == request.FollowupWorkflowInstanceId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<DocumentFollowupStatus>(request.Status, ignoreCase: true, out var parsed))
        {
            query = query.Where(f => f.Status == parsed);
        }

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
        });

        return visible.Select(GetDocumentFollowupByIdQueryHandler.MapSummary).ToList();
    }
}
