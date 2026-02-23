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
    string? UnitType = null
) : ICommand<UpdateMethodResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
