namespace Workflow.DocumentFollowups.Application.Commands;

public record RaiseDocumentFollowupLineItemDto(string DocumentType, string? Notes);

public record RaiseDocumentFollowupCommand(
    Guid RaisingWorkflowInstanceId,
    Guid RaisingPendingTaskId,
    IReadOnlyList<RaiseDocumentFollowupLineItemDto> LineItems
) : ICommand<RaiseDocumentFollowupResult>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record RaiseDocumentFollowupResult(Guid FollowupId, Guid FollowupWorkflowInstanceId);
