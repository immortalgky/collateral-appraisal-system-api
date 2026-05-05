using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.CreateProjectModelPricingAnalysis;

/// <summary>
/// Command to create a new PricingAnalysis for a ProjectModel.
/// The resulting PricingAnalysis.FinalAppraisedValue becomes the model's standard price.
/// </summary>
public record CreateProjectModelPricingAnalysisCommand(
    Guid ProjectModelId
) : ICommand<CreateProjectModelPricingAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
