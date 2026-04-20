namespace Workflow.DocumentFollowups.Application.Commands;

public record CancelDocumentFollowupCommand(Guid FollowupId, string Reason)
    : ICommand<Unit>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CancelDocumentFollowupLineItemCommand(Guid FollowupId, Guid LineItemId, string Reason) : ICommand<Unit>;