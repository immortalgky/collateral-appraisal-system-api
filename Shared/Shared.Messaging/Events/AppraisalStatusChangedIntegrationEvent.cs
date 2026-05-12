namespace Shared.Messaging.Events;

public record AppraisalStatusChangedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string? AppraisalNumber { get; init; }

    /// <summary>Internal AppraisalStatus name (e.g. "Pending", "InProgress"). Null on initial creation.</summary>
    public string? PreviousStatus { get; init; }

    /// <summary>Internal AppraisalStatus name (e.g. "Submitted", "InProgress", "Completed").</summary>
    public string Status { get; init; } = null!;
}
