namespace Common.Application.Features.Monitoring.GetTopBreaches;

/// <summary>
/// A single breached task surfaced by the top-breaches query.
/// </summary>
public record TopBreachDto(
    Guid? AppraisalId,
    string? AppraisalNumber,
    string? CustomerName,
    /// <summary>
    /// One of: "pending-internal", "pending-external", "pending-followup".
    /// </summary>
    string SectionId,
    int? OlaVarianceHours,
    string? TaskType
);
