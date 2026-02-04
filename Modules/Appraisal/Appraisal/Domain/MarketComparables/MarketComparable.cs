namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Market Comparable Aggregate Root.
/// Centralized bank-wide database of verified property transactions.
/// </summary>
public class MarketComparable : Aggregate<Guid>
{
    // Core Properties
    public string ComparableNumber { get; private set; } = null!;
    public string PropertyType { get; private set; } = null!; // Land, Building, Condo, etc.
    public string SurveyName { get; private set; } = null!;

    // Data Information
    public DateTime? InfoDateTime { get; private set; }
    public string? SourceInfo { get; private set; }

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
    }

    public static MarketComparable Create(
        string comparableNumber,
        string propertyType,
        string surveyName,
        DateTime? infoDateTime,
        string? sourceInfo,
        Guid? templateId = null,
        string? notes = null)
    {
        return new MarketComparable
        {
            Id = Guid.NewGuid(),
            ComparableNumber = comparableNumber,
            PropertyType = propertyType,
            SurveyName = surveyName,
            InfoDateTime = infoDateTime,
            SourceInfo = sourceInfo,
            TemplateId = templateId,
            Notes = notes
        };
    }

    public void Save(MarketComparableUpdateData data)
    {
        ArgumentNullException.ThrowIfNull(data.SurveyName);


        SurveyName = data.SurveyName;
        InfoDateTime = data.InfoDateTime;
        SourceInfo = data.SourceInfo;
        Notes = data.Notes;
        TemplateId = data.TemplateId;
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
        Guid documentId,
        string? title = null,
        string? description = null)
    {
        var sequence = _images.Count > 0 ? _images.Max(i => i.DisplaySequence) + 1 : 1;
        var image = MarketComparableImage.Create(Id, sequence, documentId, title, description);
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
        string? Notes);
}