using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.SaveCondoPricingAssumptions;

public class SaveCondoPricingAssumptionsCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveCondoPricingAssumptionsCommand, SaveCondoPricingAssumptionsResult>
{
    public async Task<SaveCondoPricingAssumptionsResult> Handle(
        SaveCondoPricingAssumptionsCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var assumption = appraisal.SetCondoPricingAssumption(
            command.LocationMethod,
            command.CornerAdjustment,
            command.EdgeAdjustment,
            command.PoolViewAdjustment,
            command.SouthAdjustment,
            command.OtherAdjustment,
            command.FloorIncrementEveryXFloor,
            command.FloorIncrementAmount,
            command.ForceSalePercentage);

        if (command.ModelAssumptions is { Count: > 0 })
        {
            var modelAssumptions = command.ModelAssumptions
                .Select(ma => CondoModelAssumption.Create(
                    ma.CondoModelId, ma.ModelType, ma.ModelDescription,
                    ma.UsableAreaFrom, ma.UsableAreaTo,
                    ma.StandardPrice, ma.CoverageAmount, ma.FireInsuranceCondition))
                .ToList();

            assumption.SetModelAssumptions(modelAssumptions);
        }
        else
        {
            assumption.SetModelAssumptions([]);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveCondoPricingAssumptionsResult(assumption.Id);
    }
}
