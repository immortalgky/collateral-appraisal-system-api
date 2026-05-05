using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Project.SaveProjectPricingAssumptions;

/// <summary>
/// Saves pricing assumptions for a project.
/// Branches on ProjectType:
///   Condo:           calls project.SetCondoPricingAssumption(...) with Condo-specific params.
///   LandAndBuilding: calls project.SetLandAndBuildingPricingAssumption(...) with LB-specific params.
/// ModelAssumptions: null = no change to persisted rows; empty list = clear all.
/// </summary>
public class SaveProjectPricingAssumptionsCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveProjectPricingAssumptionsCommand, SaveProjectPricingAssumptionsResult>
{
    public async Task<SaveProjectPricingAssumptionsResult> Handle(
        SaveProjectPricingAssumptionsCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        ProjectPricingAssumption assumption;

        if (project.ProjectType == ProjectType.Condo)
        {
            assumption = project.SetCondoPricingAssumption(
                command.LocationMethod,
                command.CornerAdjustment,
                command.EdgeAdjustment,
                command.PoolViewAdjustment,
                command.SouthAdjustment,
                command.OtherAdjustment,
                command.FloorIncrementEveryXFloor,
                command.FloorIncrementAmount,
                command.ForceSalePercentage);
        }
        else
        {
            assumption = project.SetLandAndBuildingPricingAssumption(
                command.LocationMethod,
                command.CornerAdjustment,
                command.EdgeAdjustment,
                command.NearGardenAdjustment,
                command.OtherAdjustment,
                command.LandIncreaseDecreaseRate,
                command.ForceSalePercentage);
        }

        // null = no change; empty list = clear all (mirrors Condo handler behaviour)
        if (command.ModelAssumptions is not null)
        {
            var validModelIds = project.Models.Select(m => m.Id).ToHashSet();

            var modelAssumptions = command.ModelAssumptions
                .Select(ma => ProjectModelAssumption.Create(
                    ma.ProjectModelId,
                    ma.ModelType,
                    ma.ModelDescription,
                    ma.UsableAreaFrom,
                    ma.UsableAreaTo,
                    ma.StandardLandPrice,
                    ma.CoverageAmount,
                    ma.FireInsuranceCondition))
                .ToList();

            assumption.ReplaceModelAssumptions(modelAssumptions, validModelIds);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveProjectPricingAssumptionsResult(assumption.Id);
    }
}
