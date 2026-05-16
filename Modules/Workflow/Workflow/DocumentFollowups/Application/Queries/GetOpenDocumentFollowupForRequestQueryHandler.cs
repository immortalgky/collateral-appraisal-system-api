using Workflow.Contracts.DocumentFollowups;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Application.Queries;

/// <summary>
/// Read-side projection used by the Integration module's external resubmit endpoint to discover
/// the open followup(s) for a request. Filters server-side to Status=Open; returns a list
/// because the data model permits multiple open followups across distinct raising tasks
/// (today's workflow never forks, so 0 or 1 is the expected case; caller treats 2+ as
/// ambiguity).
/// </summary>
public class GetOpenDocumentFollowupForRequestQueryHandler(WorkflowDbContext dbContext)
    : IRequestHandler<GetOpenDocumentFollowupForRequestQuery, IReadOnlyList<DocumentFollowupExternalDto>>
{
    public async Task<IReadOnlyList<DocumentFollowupExternalDto>> Handle(
        GetOpenDocumentFollowupForRequestQuery request,
        CancellationToken cancellationToken)
    {
        var followups = await dbContext.DocumentFollowups
            .AsNoTracking()
            .Where(f => f.RequestId == request.RequestId
                        && f.Status == DocumentFollowupStatus.Open)
            .ToListAsync(cancellationToken);

        return followups
            .Select(f => new DocumentFollowupExternalDto(
                f.Id,
                f.RequestId,
                f.Status.ToString(),
                f.FollowupWorkflowInstanceId,
                f.LineItems
                    .Select(li => new DocumentFollowupLineItemExternalDto(
                        li.Id,
                        li.DocumentType,
                        li.Status.ToString()))
                    .ToList()))
            .ToList();
    }
}
