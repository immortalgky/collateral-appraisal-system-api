using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Command to set final value for a pricing method
/// </summary>
public record SetFinalValueCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal FinalValue,
    bool? IncludeLandArea,
    decimal? LandArea,
    decimal? LandValue,
    bool? HasBuildingValue,
    decimal? BuildingValue,
    decimal? IndicatedValue
) : ICommand<SetFinalValueResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
