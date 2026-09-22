using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Command to update final value
/// </summary>
public record UpdateFinalValueCommand(
    Guid PricingAnalysisId,
    Guid FinalValueId,
    decimal FinalValue,
    bool? IncludeLandArea,
    decimal? LandArea,
    decimal? LandValue,
    bool? HasBuildingValue,
    decimal? BuildingValue,
    decimal? IndicatedValue
) : ICommand<UpdateFinalValueResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
