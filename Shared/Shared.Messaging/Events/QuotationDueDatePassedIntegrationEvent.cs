namespace Shared.Messaging.Events;

/// <summary>
/// Published by QuotationAutoCloseService after a quotation's DueDate passes.
/// Consumed by:
///   - Workflow module: archives overdue fan-out PendingTasks and resumes the workflow.
///   - Appraisal module: auto-declines CompanyQuotations for companies that never responded.
/// </summary>
public record QuotationDueDatePassedIntegrationEvent : IntegrationEvent
{
    /// <summary>The QuotationRequest that passed its due date.</summary>
    public Guid QuotationRequestId { get; init; }

    /// <summary>WorkflowInstanceId of the quotation child workflow (QuotationWorkflowInstanceId on QuotationRequest).</summary>
    public Guid? QuotationWorkflowInstanceId { get; init; }
}
