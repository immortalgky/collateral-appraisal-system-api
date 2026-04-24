namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module after expiring overdue fan-out PendingTasks.
/// Consumed by the Appraisal module to auto-decline the corresponding CompanyQuotations.
/// </summary>
public record QuotationCompaniesAutoExpiredIntegrationEvent : IntegrationEvent
{
    /// <summary>The QuotationRequest whose companies did not respond by the due date.</summary>
    public Guid QuotationRequestId { get; init; }

    /// <summary>Company IDs whose fan-out PendingTasks were archived as "Expired".</summary>
    public IReadOnlyList<Guid> ExpiredCompanyIds { get; init; } = [];
}
