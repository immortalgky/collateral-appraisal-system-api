namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Market Comparable Aggregate Root.
/// Centralized bank-wide database of verified property transactions.
/// </summary>
public class MarketComparable : Aggregate<Guid>
{
    // Core Properties
    public string? ComparableNumber { get; private set; }
    public string PropertyType { get; private set; } = null!; // Land, Building, Condo, etc.
    public string SurveyName { get; private set; } = null!;

    // Data Information
    public DateTime? InfoDateTime { get; private set; }
    public string? SourceInfo { get; private set; }

    // Important info for pricing analysis e.g. WQS, Sale grid adjustment, Direct comparison
    public decimal? OfferPrice { get; private set; }
    public decimal? OfferPriceAdjustmentPercent { get; private set; }
    public decimal? OfferPriceAdjustmentAmount { get; private set; }
    public decimal? SalePrice { get; private set; }
    public DateTime? SaleDate { get; private set; }

    // Notes
    public string? Notes { get; private set; }

    // Template Reference (optional - tracks which template was used)
    public Guid? TemplateId { get; private set; }

    // Soft Delete
    public SoftDelete SoftDelete { get; private set; } = SoftDelete.NotDeleted();

    // Child Collections (EAV Data and Images)
    private readonly List<MarketComparableData> _factorData = [];
    private readonly List<MarketComparableImage> _images = [];

    public IReadOnlyList<MarketComparableData> FactorData => _factorData.AsReadOnly();
    public IReadOnlyList<MarketComparableImage> Images => _images.AsReadOnly();

    private MarketComparable()
    {
        // For EF Core
    }

    public static MarketComparable Create(
        string propertyType,
        string surveyName,
        DateTime? infoDateTime,
        string? sourceInfo,
        Guid? templateId = null,
        string? notes = null,
        decimal? offerPrice = null,
        decimal? offerPriceAdjustmentPercent = null,
        decimal? offerPriceAdjustmentAmount = null,
        decimal? salePrice = null,
        DateTime? saleDate = null)
    {
        return new MarketComparable
        {
            Id = Guid.CreateVersion7(),
            PropertyType = propertyType,
            SurveyName = surveyName,
            InfoDateTime = infoDateTime,
            SourceInfo = sourceInfo,
            TemplateId = templateId,
            Notes = notes,
            OfferPrice = offerPrice,
            OfferPriceAdjustmentPercent = offerPriceAdjustmentPercent,
            OfferPriceAdjustmentAmount = offerPriceAdjustmentAmount,
            SalePrice = salePrice,
            SaleDate = saleDate
        };
    }

    public void SetComparableNumber(string number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ComparableNumber = number;
    }

    public void Save(MarketComparableUpdateData data)
    {
        ArgumentNullException.ThrowIfNull(data.SurveyName);

        SurveyName = data.SurveyName;
        InfoDateTime = data.InfoDateTime;
        SourceInfo = data.SourceInfo;
        Notes = data.Notes;
        TemplateId = data.TemplateId;
        OfferPrice = data.OfferPrice;
        OfferPriceAdjustmentPercent = data.OfferPriceAdjustmentPercent;
        OfferPriceAdjustmentAmount = data.OfferPriceAdjustmentAmount;
        SalePrice = data.SalePrice;
        SaleDate = data.SaleDate;
    }

    public void Delete(Guid? deletedBy)
    {
        SoftDelete = SoftDelete.Deleted(deletedBy);
    }

    public void SetTemplate(Guid templateId)
    {
        TemplateId = templateId;
    }

    // Factor Data Management
    public MarketComparableData SetFactorValue(Guid factorId, string? value, string? otherRemarks = null)
    {
        var existing = _factorData.FirstOrDefault(d => d.FactorId == factorId);
        if (existing != null)
        {
            existing.UpdateValue(value, otherRemarks);
            return existing;
        }

        var data = MarketComparableData.Create(Id, factorId, value, otherRemarks);
        _factorData.Add(data);
        return data;
    }

    public void RemoveFactorValue(Guid factorId)
    {
        var data = _factorData.FirstOrDefault(d => d.FactorId == factorId);
        if (data != null)
            _factorData.Remove(data);
    }

    // Image Management
    public MarketComparableImage AddImage(
        Guid galleryPhotoId,
        string? title = null,
        string? description = null)
    {
        var sequence = _images.Count > 0 ? _images.Max(i => i.DisplaySequence) + 1 : 1;
        var image = MarketComparableImage.Create(Id, sequence, galleryPhotoId, title, description);
        _images.Add(image);
        return image;
    }

    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image != null)
            _images.Remove(image);
    }

    public void ReorderImages(IEnumerable<(Guid ImageId, int NewSequence)> reorderCommands)
    {
        foreach (var (imageId, newSequence) in reorderCommands)
        {
            var image = _images.FirstOrDefault(i => i.Id == imageId);
            image?.UpdateSequence(newSequence);
        }
    }

    public record MarketComparableUpdateData(
        string SurveyName,
        DateTime? InfoDateTime,
        string? SourceInfo,
        Guid? TemplateId,
        string? Notes,
        decimal? OfferPrice = null,
        decimal? OfferPriceAdjustmentPercent = null,
        decimal? OfferPriceAdjustmentAmount = null,
        decimal? SalePrice = null,
        DateTime? SaleDate = null);
}