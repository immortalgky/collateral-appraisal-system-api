namespace Appraisal.Application.Features.BlockVillage.GetVillagePricingAssumptions;

public class GetVillagePricingAssumptionsQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetVillagePricingAssumptionsQuery, GetVillagePricingAssumptionsResult>
{
    public async Task<GetVillagePricingAssumptionsResult> Handle(
        GetVillagePricingAssumptionsQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        if (appraisal.VillagePricingAssumption is null)
            return new GetVillagePricingAssumptionsResult(null);

        var assumption = appraisal.VillagePricingAssumption;

        var modelAssumptions = assumption.ModelAssumptions
            .Select(ma => new VillageModelAssumptionDto(
                ma.VillageModelId, ma.ModelType, ma.ModelDescription,
                ma.UsableAreaFrom, ma.UsableAreaTo, ma.StandardLandPrice,
                ma.StandardPrice, ma.CoverageAmount, ma.FireInsuranceCondition))
            .ToList();

        var dto = new VillagePricingAssumptionDto(
            assumption.Id, assumption.AppraisalId,
            assumption.LocationMethod,
            assumption.CornerAdjustment, assumption.EdgeAdjustment,
            assumption.NearGardenAdjustment, assumption.OtherAdjustment,
            assumption.LandIncreaseDecreaseRate, assumption.ForceSalePercentage,
            modelAssumptions);

        return new GetVillagePricingAssumptionsResult(dto);
    }
}
