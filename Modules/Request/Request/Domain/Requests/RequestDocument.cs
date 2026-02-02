namespace Request.Domain.Requests;

/// <summary>
/// RequestDocument is an Entity owned by Request aggregate that links Request to the Document module.
/// Domain events are fired by the parent Request aggregate.
/// </summary>
public class RequestDocument : Entity<Guid>
{
    public Guid RequestId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string DocumentType { get; private set; } = default!;
    public string? FileName { get; private set; }
    public string? Prefix { get; private set; }
    public short? Set { get; private set; }
    public string? Notes { get; private set; }
    public string? FilePath { get; private set; }
    public string? Source { get; private set; }
    public bool IsRequired { get; private set; }
    public string? UploadedBy { get; private set; }
    public string? UploadedByName { get; private set; }
    public DateTime? UploadedAt { get; private set; }

    private RequestDocument()
    {
        // EF Core
    }

    private RequestDocument(Guid id, Guid requestId)
    {
        //Id = id;
        RequestId = requestId;
        Set = 1;
        Source = "REQUEST";
        IsRequired = false; // TODO: Implement logic to get from configuration
    }

    internal static RequestDocument Create(Guid requestId, RequestDocumentData data)
    {
        ArgumentNullException.ThrowIfNull(data.DocumentType);

        return new RequestDocument(Guid.CreateVersion7(), requestId)
        {
            DocumentId = data.DocumentId,
            DocumentType = data.DocumentType,
            FileName = data.FileName,
            Prefix = data.Prefix,
            Set = data.Set,
            Notes = data.Notes,
            FilePath = data.FilePath,
            Source = data.Source ?? "REQUEST",
            IsRequired = data.IsRequired,
            UploadedBy = data.UploadedBy,
            UploadedByName = data.UploadedByName,
            UploadedAt = data.UploadedAt
        };
    }

    /// <summary>
    /// Updates the document. Returns previous and new DocumentId for event firing by parent aggregate.
    /// </summary>
    internal (Guid? PreviousDocumentId, Guid? NewDocumentId) Update(RequestDocumentData data)
    {
        Guid? previousDocumentId = null;
        Guid? newDocumentId = null;

        // if a file changed, update the relevant fields
        if (DocumentId != data.DocumentId)
        {
            previousDocumentId = DocumentId;
            newDocumentId = data.DocumentId;

            DocumentId = data.DocumentId;
            FileName = data.FileName;
            FilePath = data.FilePath;
            Prefix = data.Prefix;

            UploadedBy = data.DocumentId.HasValue ? data.UploadedBy : null;
            UploadedByName = data.DocumentId.HasValue ? data.UploadedByName : null;
            UploadedAt = data.DocumentId.HasValue ? data.UploadedAt : null;
        }

        DocumentType = data.DocumentType;
        Set = data.Set;
        Notes = data.Notes;

        return (previousDocumentId, newDocumentId);
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DocumentType);
    }
}

public record RequestDocumentData(
    Guid? DocumentId,
    string DocumentType,
    string? FileName,
    string? Prefix,
    short? Set,
    string? Notes,
    string? FilePath,
    string? Source,
    bool IsRequired,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt
);