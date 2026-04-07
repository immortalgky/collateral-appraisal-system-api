namespace Appraisal.Application.Features.BlockCondo.GetCondoPricingAssumptions;

public class GetCondoPricingAssumptionsQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetCondoPricingAssumptionsQuery, GetCondoPricingAssumptionsResult>
{
    public async Task<GetCondoPricingAssumptionsResult> Handle(
        GetCondoPricingAssumptionsQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        if (appraisal.CondoPricingAssumption is null)
            return new GetCondoPricingAssumptionsResult(null);

        var assumption = appraisal.CondoPricingAssumption;

        var modelAssumptions = assumption.ModelAssumptions
            .Select(ma => new CondoModelAssumptionDto(
                ma.CondoModelId, ma.ModelType, ma.ModelDescription,
                ma.UsableAreaFrom, ma.UsableAreaTo,
                ma.StandardPrice, ma.CoverageAmount, ma.FireInsuranceCondition))
            .ToList();

        var dto = new CondoPricingAssumptionDto(
            assumption.Id, assumption.AppraisalId,
            assumption.LocationMethod,
            assumption.CornerAdjustment, assumption.EdgeAdjustment,
            assumption.PoolViewAdjustment, assumption.SouthAdjustment,
            assumption.OtherAdjustment,
            assumption.FloorIncrementEveryXFloor, assumption.FloorIncrementAmount,
            assumption.ForceSalePercentage,
            modelAssumptions);

        return new GetCondoPricingAssumptionsResult(dto);
    }
}
