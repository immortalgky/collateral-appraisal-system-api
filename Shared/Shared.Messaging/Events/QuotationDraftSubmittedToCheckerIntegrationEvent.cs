namespace Shared.Messaging.Events;

/// <summary>
/// Published when the Maker (ExtAdmin) submits a company quotation draft to the Checker.
/// Consumed by the Workflow module to reassign the open PendingTask from ExtAdmin to ExtAppraisalChecker.
/// </summary>
public sealed record QuotationDraftSubmittedToCheckerIntegrationEvent : IntegrationEvent
{
    public required Guid QuotationRequestId { get; init; }
    public required Guid CompanyId { get; init; }
    public required string SubmittedBy { get; init; }
}
