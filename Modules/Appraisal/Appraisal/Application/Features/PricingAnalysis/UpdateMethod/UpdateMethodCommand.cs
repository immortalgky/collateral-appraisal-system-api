using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

/// <summary>
/// Command to update an existing method
/// </summary>
public record UpdateMethodCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal? MethodValue = null,
    decimal? ValuePerUnit = null,
    string? UnitType = null,
    string? Remark = null,
    // Per-method calc mode: null leaves it unchanged, true/false sets system/manual and clears
    // the method's stale value so neither mode inherits a figure computed under the other.
    bool? UseSystemCalc = null,
    // Cost-approach component (Land / Building / LandAndBuilding / Machinery): null leaves it
    // unchanged, same convention as Remark and UseSystemCalc above.
    string? Role = null
) : ICommand<UpdateMethodResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
