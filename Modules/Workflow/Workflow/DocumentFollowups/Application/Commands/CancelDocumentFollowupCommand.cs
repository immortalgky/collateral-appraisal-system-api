namespace Workflow.DocumentFollowups.Application.Commands;

public record CancelDocumentFollowupCommand(Guid FollowupId, string Reason) : ICommand<Unit>;

public record CancelDocumentFollowupLineItemCommand(Guid FollowupId, Guid LineItemId, string Reason) : ICommand<Unit>;
