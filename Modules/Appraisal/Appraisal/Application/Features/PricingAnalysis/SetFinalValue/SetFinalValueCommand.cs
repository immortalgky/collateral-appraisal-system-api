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
    decimal FinalValueRounded,
    bool? IncludeLandArea,
    decimal? LandArea,
    decimal? AppraisalPrice,
    decimal? AppraisalPriceRounded,
    bool? HasBuildingCost,
    decimal? BuildingCost,
    decimal? AppraisalPriceWithBuilding,
    decimal? AppraisalPriceWithBuildingRounded
) : ICommand<SetFinalValueResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
