using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.ApplySelection;

/// <summary>
/// Handler applying the summary screen's whole selection in one go. All invariants, the
/// method → approach → final propagation and the single domain event live in the aggregate
/// (<see cref="Domain.Appraisals.PricingAnalysis.ApplySelection"/>).
/// </summary>
public class ApplySelectionCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<ApplySelectionCommand, ApplySelectionResult>
{
    public async Task<ApplySelectionResult> Handle(
        ApplySelectionCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        var selections = command.Selections
            .Select(s => new ApproachMethodSelection(s.ApproachId, s.MethodId))
            .ToList();

        pricingAnalysis.ApplySelection(selections, command.FinalApproachId);

        var finalApproach = pricingAnalysis.Approaches.First(a => a.Id == command.FinalApproachId);

        return new ApplySelectionResult(
            finalApproach.Id,
            finalApproach.ApproachType,
            pricingAnalysis.FinalAppraisedValue);
    }
}
