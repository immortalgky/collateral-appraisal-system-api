using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.AddMethod;

/// <summary>
/// Handler for adding a new method to an approach
/// </summary>
public class AddMethodCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
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

        // A Market method starts with the role its group implies (all-land group -> Land, otherwise
        // LandAndBuilding); the appraiser can change it. The domain cannot see the group, so it is
        // set here. A pricing analysis not anchored to a group (a reference) keeps no role.
        if (approach.ApproachType == "Market"
            && pricingAnalysis.SubjectType == PricingAnalysisSubjectType.PropertyGroup
            && pricingAnalysis.AnchorId.HasValue)
            approach.SetMethodRole(
                method.Id,
                await propertyDataService.GetDefaultMarketRoleAsync(pricingAnalysis.AnchorId.Value, cancellationToken));

        return new AddMethodResult(method.Id, method.MethodType);
    }
}
