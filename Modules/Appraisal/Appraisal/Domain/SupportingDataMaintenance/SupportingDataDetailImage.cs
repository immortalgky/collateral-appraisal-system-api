namespace Appraisal.Domain.SupportingDataMaintenance;

/// <summary>
/// A photo attached directly to a SupportingDataDetail record.
/// Stores the document reference and URL inline — no gallery middleman —
/// because SupportingData is a standalone module with no appraisal context.
/// </summary>
public class SupportingDataDetailImage : Entity<Guid>
{
    public Guid SupportingDataDetailId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string StorageUrl { get; private set; } = default!;
    public string? FileName { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public int DisplaySequence { get; private set; }

    private SupportingDataDetailImage() { /* EF */ }

    internal static SupportingDataDetailImage Create(
        Guid supportingDataDetailId,
        Guid documentId,
        string storageUrl,
        string? fileName,
        int displaySequence,
        string? title = null,
        string? description = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be empty.", nameof(documentId));

        if (string.IsNullOrWhiteSpace(storageUrl))
            throw new ArgumentException("StorageUrl cannot be empty.", nameof(storageUrl));

        return new SupportingDataDetailImage
        {
            Id = Guid.CreateVersion7(),
            SupportingDataDetailId = supportingDataDetailId,
            DocumentId = documentId,
            StorageUrl = storageUrl,
            FileName = fileName,
            DisplaySequence = displaySequence,
            Title = title,
            Description = description,
        };
    }
}
