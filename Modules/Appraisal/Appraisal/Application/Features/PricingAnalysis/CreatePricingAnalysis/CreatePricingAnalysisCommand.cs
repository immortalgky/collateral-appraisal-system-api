using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.CreatePricingAnalysis;

/// <summary>
/// Command to create a new PricingAnalysis for a PropertyGroup
/// </summary>
public record CreatePricingAnalysisCommand(
    Guid PropertyGroupId
) : ICommand<CreatePricingAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
