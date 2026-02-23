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
    decimal FinalValueRounded,
    bool? IncludeLandArea,
    decimal? LandArea,
    decimal? AppraisalPrice,
    decimal? AppraisalPriceRounded,
    bool? HasBuildingCost,
    decimal? BuildingCost,
    decimal? AppraisalPriceWithBuilding,
    decimal? AppraisalPriceWithBuildingRounded
) : ICommand<UpdateFinalValueResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
