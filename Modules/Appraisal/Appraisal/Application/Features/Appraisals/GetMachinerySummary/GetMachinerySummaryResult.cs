namespace Appraisal.Application.Features.Appraisals.GetMachinerySummary;

/// <summary>
/// Result of getting a machinery appraisal summary
/// </summary>
public record GetMachinerySummaryResult(
    Guid SummaryId,
    Guid AppraisalId,
    // Section 3.1 — General Machinery
    string? InIndustrial,
    int? SurveyedNumber,
    int? AppraisalNumber,
    int? InstalledAndUseCount,
    int? AppraisalScrapCount,
    int? AppraisedByDocumentCount,
    int? NotInstalledCount,
    string? Maintenance,
    string? Exterior,
    string? Performance,
    bool? MarketDemandAvailable,
    string? MarketDemand,
    // Section 3.3 — Rights & Legal
    string? Proprietor,
    string? Owner,
    string? MachineAddress,
    decimal? Latitude,
    decimal? Longitude,
    string? Obligation,
    string? Other
);
