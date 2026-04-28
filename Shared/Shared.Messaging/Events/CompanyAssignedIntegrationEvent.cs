namespace Shared.Messaging.Events;

public record CompanyAssignedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = default!;
    public string AssignmentMethod { get; init; } = default!;
    public string? CompletedBy { get; init; }
    public string? AppraisalNumber { get; init; }

    /// <summary>
    /// Per-appraisal fee populated by the Quotation finalize fan-out.
    /// Null when originating from a non-Quotation code path (e.g., workflow CompanySelectionActivity).
    /// Consumers should use this when present and fall back to deriving the fee from the aggregate.
    /// </summary>
    public decimal? Fee { get; init; }
}
