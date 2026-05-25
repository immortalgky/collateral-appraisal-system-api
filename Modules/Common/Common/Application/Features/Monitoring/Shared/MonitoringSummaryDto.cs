namespace Common.Application.Features.Monitoring.Shared;

/// <summary>
/// KPI snapshot for a single Monitoring tab.
/// For OLA-bearing tabs (Internal, External): all four fields are populated.
/// For non-OLA tabs (Followups, Quotations, Evaluations, Meeting): only Total is set; bucket fields are null.
/// </summary>
public record MonitoringSummaryDto(
    int Total,
    int? Breached,
    int? AtRisk,
    int? Healthy
);
