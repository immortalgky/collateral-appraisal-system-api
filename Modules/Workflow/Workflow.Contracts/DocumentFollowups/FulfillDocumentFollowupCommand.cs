namespace Workflow.Contracts.DocumentFollowups;

/// <summary>
/// System-actor variant of followup fulfillment — used by the Integration module's external
/// resubmit endpoint. Unlike SubmitDocumentFollowupCommand, it does NOT assert
/// currentUser == followup.StartedBy; the caller (the bank's resubmit endpoint) has already
/// been authenticated at the HTTP boundary.
/// </summary>
public record FulfillDocumentFollowupCommand(
    Guid FollowupId,
    IReadOnlyList<FulfillFollowupItemDto> FollowupItems,
    string Actor)
    : ICommand<Unit>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record FulfillFollowupItemDto(
    string DocumentType,
    Guid DocumentId);
