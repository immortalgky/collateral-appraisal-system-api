using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.SaveVillagePricingAssumptions;

public class SaveVillagePricingAssumptionsCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveVillagePricingAssumptionsCommand, SaveVillagePricingAssumptionsResult>
{
    public async Task<SaveVillagePricingAssumptionsResult> Handle(
        SaveVillagePricingAssumptionsCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var assumption = appraisal.SetVillagePricingAssumption(
            command.LocationMethod,
            command.CornerAdjustment,
            command.EdgeAdjustment,
            command.NearGardenAdjustment,
            command.OtherAdjustment,
            command.LandIncreaseDecreaseRate,
            command.ForceSalePercentage);

        var modelAssumptions = (command.ModelAssumptions ?? [])
            .Select(ma => VillageModelAssumption.Create(
                ma.VillageModelId, ma.ModelType, ma.ModelDescription,
                ma.UsableAreaFrom, ma.UsableAreaTo, ma.StandardLandPrice,
                ma.StandardPrice, ma.CoverageAmount, ma.FireInsuranceCondition))
            .ToList();

        assumption.SetModelAssumptions(modelAssumptions);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveVillagePricingAssumptionsResult(assumption.Id);
    }
}
