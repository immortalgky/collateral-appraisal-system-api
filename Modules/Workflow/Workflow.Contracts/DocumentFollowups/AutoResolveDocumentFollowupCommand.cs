namespace Workflow.Contracts.DocumentFollowups;

/// <summary>
/// Integration-path command: closes a document followup without per-item fulfillment checks.
/// Used by RequestResubmittedIntegrationEventConsumer when the bank's resubmit re-syncs
/// all documents wholesale and we want to auto-resolve rather than validate item coverage.
/// </summary>
public record AutoResolveDocumentFollowupCommand(
    Guid FollowupId,
    string Actor,
    string Reason)
    : ICommand<Unit>, ITransactionalCommand<IWorkflowUnitOfWork>;
