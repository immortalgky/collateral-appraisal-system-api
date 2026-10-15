namespace Appraisal.Domain.Appraisals.Hypothesis;

/// <summary>
/// L&amp;B only: which building property a house model is built like, and an optional total that
/// overrides the computed construction cost for that model (FSD C21).
///
/// The per-house figure is NOT stored here — it is read live from the building property
/// (its Final Cost Value) at every preview/save, so an edit on the building-detail page flows
/// through without touching the hypothesis. No FK to AppraisalProperties on purpose: the
/// property belongs to another aggregate, and a mapping whose building is gone simply reads
/// as "no building selected" (cost 0 unless <see cref="TotalCost"/> is set).
/// </summary>
public class HypothesisModelBuildingMapping : Entity<Guid>
{
    public Guid HypothesisAnalysisId { get; private set; }

    /// <summary>Matches LandBuildingUnitRow.ModelName (case-insensitive, like the model aggregate).</summary>
    public string ModelName { get; private set; } = null!;

    /// <summary>The building property whose Final Cost Value is the per-house cost. Null = not chosen.</summary>
    public Guid? AppraisalPropertyId { get; private set; }

    /// <summary>The appraiser's typed-over total for the model (C21). Null = per-house × unit count.</summary>
    public decimal? TotalCost { get; private set; }

    private HypothesisModelBuildingMapping() { }

    internal static HypothesisModelBuildingMapping Create(
        Guid analysisId, string modelName, Guid? appraisalPropertyId, decimal? totalCost)
    {
        return new HypothesisModelBuildingMapping
        {
            Id = Guid.CreateVersion7(),
            HypothesisAnalysisId = analysisId,
            ModelName = modelName,
            AppraisalPropertyId = appraisalPropertyId,
            TotalCost = totalCost
        };
    }

    internal void Update(Guid? appraisalPropertyId, decimal? totalCost)
    {
        AppraisalPropertyId = appraisalPropertyId;
        TotalCost = totalCost;
    }

    /// <summary>
    /// Deep-clone for CI carry-forward. A building with no counterpart in the new appraisal is
    /// dropped to null (the model then warns as unmapped); the typed-over total is kept.
    /// </summary>
    internal static HypothesisModelBuildingMapping CloneForAnalysis(
        HypothesisModelBuildingMapping source, Guid newAnalysisId, IReadOnlyDictionary<Guid, Guid>? propertyIdMap)
    {
        Guid? newPropertyId = source.AppraisalPropertyId is { } oldId
                              && propertyIdMap is not null
                              && propertyIdMap.TryGetValue(oldId, out var mapped)
            ? mapped
            : null;

        return Create(newAnalysisId, source.ModelName, newPropertyId, source.TotalCost);
    }
}
