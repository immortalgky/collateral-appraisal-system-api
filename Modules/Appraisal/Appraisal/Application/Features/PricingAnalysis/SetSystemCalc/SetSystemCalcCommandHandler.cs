using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.SetSystemCalc;

public class SetSystemCalcCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<SetSystemCalcCommand, SetSystemCalcResult>
{
    public async Task<SetSystemCalcResult> Handle(
        SetSystemCalcCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis == null)
            throw new InvalidOperationException($"Pricing analysis with ID '{command.PricingAnalysisId}' not found");

        pricingAnalysis.SetUseSystemCalc(command.UseSystemCalc);

        foreach (var approach in pricingAnalysis.Approaches)
        {
            approach.Unselect();
            approach.ClearValue();

            foreach (var method in approach.Methods)
            {
                method.SetAsUnselected();
                method.ClearValue();
                method.SetUseSystemCalc(command.UseSystemCalc);
            }
        }

        pricingAnalysis.ClearFinalValues();

        return new SetSystemCalcResult(
            pricingAnalysis.Id,
            pricingAnalysis.UseSystemCalc);
    }
}