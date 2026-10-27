using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;

/// <summary>
/// Command to record a hand-entered Cost-approach breakdown for a pricing method.
/// The caller supplies only the land rate and the rounded appraisal price; land area,
/// land value and building value are derived server-side.
/// A null <paramref name="LandRatePerSqWa"/> clears the breakdown.
/// <paramref name="BuildingValue"/> is read only for a Cost method with Role = LandAndBuilding,
/// where it is required alongside a rate.
/// </summary>
public record SetManualCostBreakdownCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal? LandRatePerSqWa,
    decimal? IndicatedValue,
    decimal? BuildingValue = null
) : ICommand<SetManualCostBreakdownResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
