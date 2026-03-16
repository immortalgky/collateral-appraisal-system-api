using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateOrUpdateMachinerySummary;

/// <summary>
/// Command to create or update the machinery appraisal summary for an appraisal
/// </summary>
public record CreateOrUpdateMachinerySummaryCommand(
    Guid AppraisalId,
    // Section 3.1 — General Machinery
    string? InIndustrial = null,
    int? SurveyedNumber = null,
    int? AppraisalNumber = null,
    int? InstalledAndUseCount = null,
    int? AppraisalScrapCount = null,
    int? AppraisedByDocumentCount = null,
    int? NotInstalledCount = null,
    string? Maintenance = null,
    string? Exterior = null,
    string? Performance = null,
    bool? MarketDemandAvailable = null,
    string? MarketDemand = null,
    // Section 3.3 — Rights & Legal
    string? Proprietor = null,
    string? Owner = null,
    string? MachineAddress = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? Obligation = null,
    string? Other = null
) : ICommand<CreateOrUpdateMachinerySummaryResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
