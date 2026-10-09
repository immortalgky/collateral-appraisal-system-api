using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

/// <summary>
/// Building Final Cost Values for an L&amp;B hypothesis — shared by Save and Preview so both price
/// house models the same way and both refuse a building from outside the group.
/// </summary>
internal static class HypothesisBuildingValues
{
    /// <summary>Per-building Final Cost Value of the group; empty when the subject is not a group.</summary>
    public static async Task<IReadOnlyDictionary<Guid, decimal>> LoadAsync(
        PricingAnalysisSubjectType subjectType,
        Guid? anchorId,
        PricingPropertyDataService propertyDataService,
        CancellationToken cancellationToken)
    {
        if (subjectType != PricingAnalysisSubjectType.PropertyGroup || !anchorId.HasValue)
            return new Dictionary<Guid, decimal>();

        return await propertyDataService.GetBuildingFinalCostValuesAsync(anchorId.Value, cancellationToken);
    }

    /// <summary>
    /// The FE lists only the group's buildings, but the API is the gate: a property id from another
    /// group or appraisal must never price a model off someone else's building. Such a mapping is
    /// read as "no building selected" (the model then costs 0 and the FE warns), rather than
    /// rejected: a building later moved out of the group or deleted leaves exactly that id behind in
    /// the saved mappings, and throwing would block every Save and Preview until the user noticed
    /// and remapped a value the dropdown cannot even show.
    /// </summary>
    public static IReadOnlyList<ModelBuildingMappingInput>? DropBuildingsOutsideGroup(
        IReadOnlyList<ModelBuildingMappingInput>? mappings,
        IReadOnlyDictionary<Guid, decimal> buildingValues)
    {
        return mappings?
            .Select(m => m.AppraisalPropertyId is { } id && !buildingValues.ContainsKey(id)
                ? m with { AppraisalPropertyId = null }
                : m)
            .ToList();
    }
}
