using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Appraisals.Hypothesis.Uploads;

namespace Appraisal.Domain.Appraisals.Hypothesis;

/// <summary>
/// Hypothesis / Residual pricing analysis — 1:1 child of PricingAnalysisMethod.
/// Polymorphic via <see cref="Variant"/>: either LandBuilding (C-fields) or Condominium (E-fields).
/// </summary>
public class HypothesisAnalysis : Entity<Guid>
{
    private readonly List<HypothesisUnitDetailUpload> _uploads = [];
    private readonly List<LandBuildingUnitRow> _landBuildingUnitRows = [];
    private readonly List<CondominiumUnitRow> _condominiumUnitRows = [];
    private readonly List<HypothesisCostItem> _costItems = [];

    public IReadOnlyList<HypothesisUnitDetailUpload> Uploads => _uploads.AsReadOnly();
    public IReadOnlyList<LandBuildingUnitRow> LandBuildingUnitRows => _landBuildingUnitRows.AsReadOnly();
    public IReadOnlyList<CondominiumUnitRow> CondominiumUnitRows => _condominiumUnitRows.AsReadOnly();
    public IReadOnlyList<HypothesisCostItem> CostItems => _costItems.AsReadOnly();

    public Guid PricingMethodId { get; private set; }
    public HypothesisVariant Variant { get; private set; }

    // ── Owned-entity summary blocks ───────────────────────────────────────
    /// <summary>Non-null only when Variant == LandBuilding.</summary>
    public LandBuildingSummary? LandBuildingSummary { get; private set; }

    /// <summary>Non-null only when Variant == Condominium.</summary>
    public CondominiumSummary? CondominiumSummary { get; private set; }

    // ── Invariant: exactly one IsActive upload (or zero) ─────────────────
    public HypothesisUnitDetailUpload? ActiveUpload =>
        _uploads.SingleOrDefault(u => u.IsActive);

    private HypothesisAnalysis() { }

    // ── Factory ───────────────────────────────────────────────────────────

    public static HypothesisAnalysis Create(Guid pricingMethodId, HypothesisVariant variant)
    {
        var analysis = new HypothesisAnalysis
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            Variant = variant
        };

        // Initialise the correct summary block; leave the other null.
        if (variant == HypothesisVariant.LandBuilding)
        {
            analysis.LandBuildingSummary = new LandBuildingSummary
            {
                ContingencyPercent = 3m,           // FSD C35
                ProjectContingencyPercent = 3m     // FSD C61
            };
        }
        else
        {
            analysis.CondominiumSummary = new CondominiumSummary
            {
                HardCostContingencyPercent = 3m,   // FSD E25
                TransferFeePercent = 1m            // FSD E46
            };
        }

        return analysis;
    }

    // ── Upload management ─────────────────────────────────────────────────

    /// <summary>
    /// Registers a new upload and deactivates any currently-active upload.
    /// Returns the new upload entity.
    /// </summary>
    public HypothesisUnitDetailUpload AddUpload(string fileName, DateTime uploadedAt, int rowCount)
    {
        // Deactivate prior active upload
        foreach (var u in _uploads.Where(u => u.IsActive))
            u.Deactivate();

        var upload = HypothesisUnitDetailUpload.Create(Id, fileName, uploadedAt, rowCount);
        _uploads.Add(upload);
        return upload;
    }

    /// <summary>
    /// Removes an upload and all unit rows that belong to it.
    /// If the upload was active, no auto-promotion — user must re-upload.
    /// </summary>
    public void RemoveUpload(Guid uploadId)
    {
        var upload = _uploads.FirstOrDefault(u => u.Id == uploadId)
                     ?? throw new InvalidOperationException($"Upload {uploadId} not found.");

        // Remove rows that belong to this upload so EF tracks the deletes.
        _landBuildingUnitRows.RemoveAll(r => r.UploadId == uploadId);
        _condominiumUnitRows.RemoveAll(r => r.UploadId == uploadId);

        _uploads.Remove(upload);
    }

    // ── Unit rows ─────────────────────────────────────────────────────────

    public void ReplaceLandBuildingRows(IEnumerable<LandBuildingUnitRow> rows)
    {
        AssertVariant(HypothesisVariant.LandBuilding);
        _landBuildingUnitRows.Clear();
        _landBuildingUnitRows.AddRange(rows);
    }

    public void ReplaceCondominiumRows(IEnumerable<CondominiumUnitRow> rows)
    {
        AssertVariant(HypothesisVariant.Condominium);
        _condominiumUnitRows.Clear();
        _condominiumUnitRows.AddRange(rows);
    }

    // ── Summary updates ───────────────────────────────────────────────────

    public void UpdateLandBuildingSummary(LandBuildingSummary summary)
    {
        AssertVariant(HypothesisVariant.LandBuilding);
        LandBuildingSummary = summary;
    }

    public void UpdateCondominiumSummary(CondominiumSummary summary)
    {
        AssertVariant(HypothesisVariant.Condominium);
        CondominiumSummary = summary;
    }

    // ── Cost items ────────────────────────────────────────────────────────

    public HypothesisCostItem AddCostItem(
        HypothesisCostCategory category,
        CostItemKind kind,
        string description,
        int displaySequence,
        string? modelName = null)
    {
        ValidateCostCategory(category);

        var item = HypothesisCostItem.Create(Id, category, kind, description, displaySequence, modelName);
        _costItems.Add(item);
        return item;
    }

    public void RemoveCostItem(Guid itemId)
    {
        var item = _costItems.FirstOrDefault(i => i.Id == itemId)
                   ?? throw new InvalidOperationException($"Cost item {itemId} not found.");
        _costItems.Remove(item);
    }

    public void ClearCostItemsByCategory(HypothesisCostCategory category)
    {
        _costItems.RemoveAll(i => i.Category == category);
    }

    public void ClearAllCostItems()
    {
        _costItems.Clear();
    }

    // ── Invariant helpers ─────────────────────────────────────────────────

    private void AssertVariant(HypothesisVariant expected)
    {
        if (Variant != expected)
            throw new InvalidOperationException(
                $"Operation requires variant {expected} but analysis has variant {Variant}.");
    }

    private void ValidateCostCategory(HypothesisCostCategory category)
    {
        var lbCategories = new[]
        {
            HypothesisCostCategory.CostOfBuilding,
            HypothesisCostCategory.ProjectDevCost,
            HypothesisCostCategory.ProjectCost,
            HypothesisCostCategory.GovernmentTax
        };
        var condoCategories = new[]
        {
            HypothesisCostCategory.HardCost,
            HypothesisCostCategory.SoftCost,
            HypothesisCostCategory.CondoGovTax
        };

        var valid = Variant == HypothesisVariant.LandBuilding
            ? lbCategories
            : condoCategories;

        if (!valid.Contains(category))
            throw new InvalidOperationException(
                $"Cost category {category} is not valid for variant {Variant}.");
    }
}
