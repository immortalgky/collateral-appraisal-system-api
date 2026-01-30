using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SelectMethod;

/// <summary>
/// Command to select a method as the primary method (setting others as Alternative)
/// </summary>
public record SelectMethodCommand(
    Guid PricingAnalysisId,
    Guid MethodId
) : ICommand<SelectMethodResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
