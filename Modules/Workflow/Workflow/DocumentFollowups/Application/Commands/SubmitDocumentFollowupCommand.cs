namespace Workflow.DocumentFollowups.Application.Commands;

public record SubmitDocumentFollowupCommand(Guid FollowupId) : ICommand<Unit>;
