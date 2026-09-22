using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

/// <summary>
/// Handler for updating a method
/// </summary>
public class UpdateMethodCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<UpdateMethodCommand, UpdateMethodResult>
{
    public async Task<UpdateMethodResult> Handle(
        UpdateMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis == null)
            throw new InvalidOperationException($"Pricing analysis with ID '{command.PricingAnalysisId}' not found");

        PricingAnalysisMethod? method = null;
        PricingAnalysisApproach? parentApproach = null;

        foreach (var approach in pricingAnalysis.Approaches)
        {
            method = approach.Methods.FirstOrDefault(m => m.Id == command.MethodId);
            if (method != null)
            {
                parentApproach = approach;
                break;
            }
        }

        if (method == null)
            throw new InvalidOperationException($"Method with ID '{command.MethodId}' not found");

        // UseSystemCalc: null leaves it unchanged (same convention as Remark below). Applied BEFORE
        // SetValue: a flip clears the value, so a MethodValue sent in the same request must land
        // after it. SetCalcMode is the single domain operation for a flip — flag + unselect + clear value + drop manual land
        // area — see its remarks on PricingAnalysisMethod for why that bundle lives in the
        // aggregate rather than here. Called through the aggregate root so the deselection also
        // clears the approach/final figure it was carrying (RecalculateRollup below would keep it).
        if (command.UseSystemCalc.HasValue)
        {
            pricingAnalysis.SetMethodCalcMode(method.Id, command.UseSystemCalc.Value);
        }

        // Only call SetValue if at least one parameter is provided
        if (command.MethodValue.HasValue || command.ValuePerUnit.HasValue || command.UnitType != null)
        {
            method.SetValue(
                command.MethodValue ?? method.MethodValue ?? 0,
                command.ValuePerUnit ?? method.ValuePerUnit,
                command.UnitType ?? method.UnitType);
        }

        // Remark has its own condition: null means "not provided, leave unchanged"; empty
        // string means "clear it" (mirrors the not-provided semantics UnitType already has
        // above — there is no separate sentinel type for clearing).
        if (command.Remark != null)
        {
            method.SetRemark(command.Remark.Length == 0 ? null : command.Remark);
        }

        // Role: null leaves it unchanged (same convention as Remark and UseSystemCalc above).
        // Through the approach, which rejects a role that would count a Cost component twice.
        if (command.Role != null)
        {
            parentApproach!.SetMethodRole(method.Id, command.Role);
        }

        // Roll the new method value up through approach → analysis (null-safe, idempotent).
        pricingAnalysis.RecalculateRollup();

        return new UpdateMethodResult(
            method.Id,
            method.MethodType,
            method.MethodValue,
            method.ValuePerUnit,
            method.UnitType,
            method.Remark,
            method.UseSystemCalc,
            parentApproach!.ApproachValue,
            pricingAnalysis.FinalAppraisedValue);
    }
}
