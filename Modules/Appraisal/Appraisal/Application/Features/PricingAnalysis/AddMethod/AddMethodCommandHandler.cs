using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.AddMethod;

/// <summary>
/// Handler for adding a new method to an approach
/// </summary>
public class AddMethodCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<AddMethodCommand, AddMethodResult>
{
    public async Task<AddMethodResult> Handle(
        AddMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis == null)
            throw new InvalidOperationException($"Pricing analysis with ID '{command.PricingAnalysisId}' not found");

        var approach = pricingAnalysis.Approaches.FirstOrDefault(a => a.Id == command.ApproachId);

        if (approach == null)
            throw new InvalidOperationException($"Approach with ID '{command.ApproachId}' not found");

        var method = approach.AddMethod(command.MethodType, command.Status);

        return new AddMethodResult(method.Id, method.MethodType);
    }
}
