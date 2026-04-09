using Shared.Identity;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Application.Queries;

public record GetDocumentFollowupByIdQuery(Guid FollowupId) : IRequest<DocumentFollowupDto?>;

public class GetDocumentFollowupByIdQueryHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDocumentFollowupByIdQuery, DocumentFollowupDto?>
{
    public async Task<DocumentFollowupDto?> Handle(GetDocumentFollowupByIdQuery request, CancellationToken cancellationToken)
    {
        var actor = currentUser.UserId?.ToString() ?? currentUser.Username;
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

        return Map(followup);
    }

    internal static DocumentFollowupDto Map(DocumentFollowup f) => new(
        Id: f.Id,
        ParentAppraisalId: f.AppraisalId,
        RequestId: f.RequestId,
        RaisingWorkflowInstanceId: f.RaisingWorkflowInstanceId,
        RaisingTaskId: f.RaisingPendingTaskId,
        RaisingActivityId: f.RaisingActivityId,
        // DisplayName is best-effort: no user-lookup service exists yet in this module.
        // Frontend must tolerate a null DisplayName until one is wired up.
        RaisedBy: new DocumentFollowupUserRef(f.RaisingUserId, DisplayName: null),
        FollowupWorkflowInstanceId: f.FollowupWorkflowInstanceId,
        Status: f.Status.ToString(),
        CancellationReason: f.CancellationReason,
        RaisedAt: f.RaisedAt,
        ResolvedAt: f.ResolvedAt,
        LineItems: f.LineItems.Select(li => new DocumentFollowupLineItemDto(
            li.Id, li.DocumentType, li.Notes, li.Status.ToString(),
            li.Reason, li.DocumentId, li.ResolvedAt)).ToList());

    internal static DocumentFollowupSummaryDto MapSummary(DocumentFollowup f) => new(
        Id: f.Id,
        ParentAppraisalId: f.AppraisalId,
        RequestId: f.RequestId,
        RaisingWorkflowInstanceId: f.RaisingWorkflowInstanceId,
        RaisingTaskId: f.RaisingPendingTaskId,
        RaisingActivityId: f.RaisingActivityId,
        RaisedBy: new DocumentFollowupUserRef(f.RaisingUserId, DisplayName: null),
        FollowupWorkflowInstanceId: f.FollowupWorkflowInstanceId,
        Status: f.Status.ToString(),
        RaisedAt: f.RaisedAt,
        ResolvedAt: f.ResolvedAt,
        LineItemCount: f.LineItems.Count,
        PendingCount: f.LineItems.Count(li => li.Status == DocumentFollowupLineItemStatus.Pending));
}
