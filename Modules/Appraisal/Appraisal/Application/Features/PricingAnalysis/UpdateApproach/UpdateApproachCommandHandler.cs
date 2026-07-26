namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

/// <summary>
/// Handler for updating an approach
/// </summary>
public class UpdateApproachCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<UpdateApproachCommand, UpdateApproachResult>
{
    public async Task<UpdateApproachResult> Handle(
        UpdateApproachCommand command,
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

        // Weight only — no value propagation. ApproachValue is derived from the selected method
        // and is never client-supplied, so nothing here can change the rollup: no SetValue, and
        // no RollUpFinalFromSelectedApproach. The values returned below are read-only echoes of
        // whatever the last method-level save derived.

        return new UpdateApproachResult(
            approach.Id,
            approach.ApproachType,
            approach.ApproachValue,
            pricingAnalysis.FinalAppraisedValue);
    }
}