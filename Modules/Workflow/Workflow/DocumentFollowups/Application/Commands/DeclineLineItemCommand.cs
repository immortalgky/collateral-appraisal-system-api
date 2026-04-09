namespace Workflow.DocumentFollowups.Application.Commands;

public record DeclineLineItemCommand(Guid FollowupId, Guid LineItemId, string Reason) : ICommand<Unit>;
