using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.AddMethod;

/// <summary>
/// Command to add a new method to an approach
/// </summary>
public record AddMethodCommand(
    Guid PricingAnalysisId,
    Guid ApproachId,
    string MethodType,
    string Status = "Selected"
) : ICommand<AddMethodResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
