using Auth.Contracts.Users;
using Shared.Identity;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Application.Queries;

public record GetDocumentFollowupByIdQuery(Guid FollowupId) : IRequest<DocumentFollowupDto?>;

public class GetDocumentFollowupByIdQueryHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IUserLookupService userLookupService)
    : IRequestHandler<GetDocumentFollowupByIdQuery, DocumentFollowupDto?>
{
    public async Task<DocumentFollowupDto?> Handle(GetDocumentFollowupByIdQuery request,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Username;
        if (string.IsNullOrWhiteSpace(actor))
            throw new UnauthorizedAccessException("User not authenticated");

        var followup = await dbContext.DocumentFollowups
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FollowupId, cancellationToken);

        if (followup is null)
            return null;

        // Authorization: caller must be either the raising user (checker) or the assignee of
        // the followup workflow (= the parent workflow's StartedBy, i.e. the request maker).
        var isRaisingUser = string.Equals(followup.RaisingUserId, actor, StringComparison.OrdinalIgnoreCase);
        var isFollowupAssignee = false;
        if (!isRaisingUser && followup.FollowupWorkflowInstanceId.HasValue)
        {
            var startedBy = await dbContext.WorkflowInstances
                .AsNoTracking()
                .Where(w => w.Id == followup.FollowupWorkflowInstanceId.Value)
                .Select(w => w.StartedBy)
                .FirstOrDefaultAsync(cancellationToken);
            isFollowupAssignee = !string.IsNullOrEmpty(startedBy) &&
                                 string.Equals(startedBy, actor, StringComparison.OrdinalIgnoreCase);
        }

        if (!isRaisingUser && !isFollowupAssignee)
            throw new UnauthorizedAccessException("Not authorized to view this document followup");

        var userMap = await userLookupService.GetByUsernamesAsync(
            new[] { followup.RaisingUserId }, cancellationToken);

        return Map(followup, userMap);
    }

    internal static DocumentFollowupDto Map(
        DocumentFollowup f,
        IReadOnlyDictionary<string, UserLookupDto> userMap)
    {
        return new DocumentFollowupDto(
            f.Id,
            f.AppraisalId,
            f.RequestId,
            f.RaisingWorkflowInstanceId,
            f.RaisingPendingTaskId,
            f.RaisingActivityId,
            BuildUserRef(f.RaisingUserId, userMap),
            f.FollowupWorkflowInstanceId,
            f.Status.ToString(),
            f.CancellationReason,
            f.RaisedAt,
            f.ResolvedAt,
            f.LineItems.Select(li => new DocumentFollowupLineItemDto(
                li.Id, li.DocumentType, li.Notes, li.Status.ToString(),
                li.Reason, li.DocumentId, li.ResolvedAt)).ToList());
    }

    internal static DocumentFollowupSummaryDto MapSummary(
        DocumentFollowup f,
        IReadOnlyDictionary<string, UserLookupDto> userMap)
    {
        return new DocumentFollowupSummaryDto(
            f.Id,
            f.AppraisalId,
            f.RequestId,
            f.RaisingWorkflowInstanceId,
            f.RaisingPendingTaskId,
            f.RaisingActivityId,
            BuildUserRef(f.RaisingUserId, userMap),
            f.FollowupWorkflowInstanceId,
            f.Status.ToString(),
            f.RaisedAt,
            f.ResolvedAt,
            f.LineItems.Count,
            f.LineItems.Count(li => li.Status == DocumentFollowupLineItemStatus.Pending));
    }

    internal static DocumentFollowupUserRef BuildUserRef(
        string? userId,
        IReadOnlyDictionary<string, UserLookupDto> userMap)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new DocumentFollowupUserRef(string.Empty, string.Empty);

        if (userMap.TryGetValue(userId, out var user))
        {
            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = userId;
            return new DocumentFollowupUserRef(userId, displayName);
        }

        return new DocumentFollowupUserRef(userId, userId);
    }
}