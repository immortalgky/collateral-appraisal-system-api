namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Per-appraisal appendix entry, pre-generated from AppendixType configuration.
/// Each appendix can hold multiple documents and a layout setting for report generation.
/// </summary>
public class AppraisalAppendix : Aggregate<Guid>
{
    private readonly List<AppendixDocument> _documents = [];
    public IReadOnlyList<AppendixDocument> Documents => _documents.AsReadOnly();

    public Guid AppraisalId { get; private set; }
    public Guid AppendixTypeId { get; private set; }
    public int SortOrder { get; private set; }
    public int LayoutColumns { get; private set; }

    private AppraisalAppendix()
    {
        // For EF Core
    }

    public static AppraisalAppendix Create(
        Guid appraisalId,
        Guid appendixTypeId,
        int sortOrder)
    {
        return new AppraisalAppendix
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AppendixTypeId = appendixTypeId,
            SortOrder = sortOrder,
            LayoutColumns = 1
        };
    }

    public void UpdateLayout(int columns)
    {
        if (columns is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(columns), "Layout columns must be 1, 2, or 3.");

        LayoutColumns = columns;
    }

    public AppendixDocument AddDocument(
        Guid galleryPhotoId,
        int displaySequence)
    {
        var document = AppendixDocument.Create(
            Id, galleryPhotoId, displaySequence);
        _documents.Add(document);
        return document;
    }

    public void RemoveDocument(Guid documentId)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);
        if (document != null) _documents.Remove(document);
    }
}