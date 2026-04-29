namespace Appraisal.Application.Features.Project.GetProjectPricingAssumptions;

/// <summary>
/// Returns pricing assumptions for a project (Condo or LandAndBuilding).
/// When no ModelAssumptions have been persisted, falls back to deriving them from
/// the project's Models collection — matching the behaviour of the old Condo handler.
/// For Condo projects the CoverageAmount fall-back is derived from FireInsuranceCondition.
/// </summary>
public class GetProjectPricingAssumptionsQueryHandler(
    IProjectRepository projectRepository
) : IQueryHandler<GetProjectPricingAssumptionsQuery, GetProjectPricingAssumptionsResult>
{

    public async Task<GetProjectPricingAssumptionsResult> Handle(
        GetProjectPricingAssumptionsQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(query.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {query.AppraisalId}");

        var assumption = project.PricingAssumption;
        var isCondo = project.ProjectType == ProjectType.Condo;
        var projectTypeName = project.ProjectType.ToString();

        // No assumption row yet — return a shell DTO derived from the project's models
        // so the FE Pricing Assumption tab can populate the model rows on first load.
        // If there are no models either, return null (nothing to show).
        if (assumption is null)
        {
            if (project.Models.Count == 0)
                return new GetProjectPricingAssumptionsResult(null);

            var shellDto = new ProjectPricingAssumptionDto(
                Guid.Empty,          // sentinel: not yet persisted
                project.Id,
                projectTypeName,
                LocationMethod: null,
                CornerAdjustment: null,
                EdgeAdjustment: null,
                OtherAdjustment: null,
                ForceSalePercentage: null,
                PoolViewAdjustment: null,
                SouthAdjustment: null,
                FloorIncrementEveryXFloor: null,
                FloorIncrementAmount: null,
                NearGardenAdjustment: null,
                LandIncreaseDecreaseRate: null,
                ModelAssumptions: DeriveFromModels(project.Models, isCondo));

            return new GetProjectPricingAssumptionsResult(shellDto);
        }

        // Build model assumption list.
        // If persisted model-assumptions exist: use them.
        // Otherwise: derive from project models (read-side parity with old Condo handler).
        var modelAssumptions = assumption.ModelAssumptions.Count > 0
            ? assumption.ModelAssumptions
                .Select(ma => new ProjectModelAssumptionDto(
                    ma.ProjectModelId,
                    ma.ModelType,
                    ma.ModelDescription,
                    ma.UsableAreaFrom,
                    ma.UsableAreaTo,
                    ma.StandardPrice,
                    ma.StandardLandPrice,
                    ma.CoverageAmount,
                    ma.FireInsuranceCondition))
                .ToList()
            : DeriveFromModels(project.Models, isCondo);

        var dto = new ProjectPricingAssumptionDto(
            assumption.Id,
            project.Id,
            projectTypeName,
            assumption.LocationMethod,
            assumption.CornerAdjustment,
            assumption.EdgeAdjustment,
            assumption.OtherAdjustment,
            assumption.ForceSalePercentage,
            // Condo-only (null for LB)
            assumption.PoolViewAdjustment,
            assumption.SouthAdjustment,
            assumption.FloorIncrementEveryXFloor,
            assumption.FloorIncrementAmount,
            // LB-only (null for Condo)
            assumption.NearGardenAdjustment,
            assumption.LandIncreaseDecreaseRate,
            modelAssumptions);

        return new GetProjectPricingAssumptionsResult(dto);
    }

    private static List<ProjectModelAssumptionDto> DeriveFromModels(
        IReadOnlyList<ProjectModel> models, bool isCondo) =>
        models.Select(m => new ProjectModelAssumptionDto(
            m.Id,
            m.ModelName,
            m.ModelDescription,
            m.UsableAreaMin,
            m.UsableAreaMax,
            m.StandardPrice,
            // StandardLandArea is LB-only; null for Condo
            isCondo ? null : m.StandardLandArea,
            // CoverageAmount: Condo derives from FireInsuranceCondition; LB has no model-level coverage
            isCondo ? LookupCoverageAmount(m.FireInsuranceCondition) : null,
            m.FireInsuranceCondition))
        .ToList();

    private static decimal? LookupCoverageAmount(string? condition)
        => CoverageByCondition.Lookup(condition);
}
