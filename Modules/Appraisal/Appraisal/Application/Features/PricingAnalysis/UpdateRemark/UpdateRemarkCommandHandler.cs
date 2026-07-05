namespace Appraisal.Application.Features.PricingAnalysis.UpdateRemark;

public class UpdateRemarkCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<UpdateRemarkCommand, UpdateRemarkResult>
{
    public async Task<UpdateRemarkResult> Handle(UpdateRemarkCommand command, CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        pricingAnalysis.SetRemark(command.Remark);

        return new UpdateRemarkResult(pricingAnalysis.Id, pricingAnalysis.Remark);
    }
}
