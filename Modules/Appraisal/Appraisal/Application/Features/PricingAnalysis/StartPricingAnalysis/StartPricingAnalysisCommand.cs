using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.StartPricingAnalysis;

/// <summary>
/// Command to start pricing analysis (change status from Draft to InProgress)
/// </summary>
public record StartPricingAnalysisCommand(
    Guid Id
) : ICommand<StartPricingAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
