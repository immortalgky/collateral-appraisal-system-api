namespace Appraisal.Application.Features.PricingAnalysis.SelectMethod;

/// <summary>
/// Handler for selecting a method as primary
/// </summary>
public class SelectMethodCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<SelectMethodCommand, SelectMethodResult>
{
    public async Task<SelectMethodResult> Handle(
        SelectMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis == null)
            throw new InvalidOperationException($"Pricing analysis with ID '{command.PricingAnalysisId}' not found");

        PricingAnalysisMethod? targetMethod = null;
        PricingAnalysisApproach? parentApproach = null;

        // Find the target method and its parent approach
        foreach (var approach in pricingAnalysis.Approaches)
        {
            targetMethod = approach.Methods.FirstOrDefault(m => m.Id == command.MethodId);
            if (targetMethod != null)
            {
                parentApproach = approach;
                break;
            }
        }

        if (targetMethod == null)
            throw new InvalidOperationException($"Method with ID '{command.MethodId}' not found");

        // Set a target method as Selected
        targetMethod.SetAsSelected();

        // Set all other methods in the same approach as Alternative
        foreach (var method in parentApproach!.Methods)
        {
            if (method.Id != command.MethodId)
            {
                method.SetAsUnselected();
            }
        }

        return new SelectMethodResult(
            targetMethod.Id,
            targetMethod.MethodType);
    }
}
