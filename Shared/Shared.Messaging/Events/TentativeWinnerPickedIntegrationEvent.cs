namespace Shared.Messaging.Events;

/// <summary>
/// Published when a tentative winner is picked from the shortlist.
/// Triggers notifications to the admin pool and to the winning company.
/// </summary>
public record TentativeWinnerPickedIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid RequestId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid CompanyQuotationId { get; init; }
    public Guid PickedBy { get; init; }

    /// <summary>"RM" or "Admin"</summary>
    public string Role { get; init; } = default!;
}
