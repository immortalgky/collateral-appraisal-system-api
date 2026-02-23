namespace Request.Domain.RequestTitles;

/// <summary>
/// TitleDocument is an Entity owned by RequestTitle aggregate that links RequestTitle to the Document module.
/// </summary>
public class TitleDocument : Entity<Guid>
{
    public Guid TitleId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string? DocumentType { get; private set; }
    public string? FileName { get; private set; }
    public string? Prefix { get; private set; }
    public int Set { get; private set; }
    public string? Notes { get; private set; }
    public string? FilePath { get; private set; }
    public bool IsRequired { get; private set; }
    public string? UploadedBy { get; private set; }
    public string? UploadedByName { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private TitleDocument()
    {
        // EF Core
    }

    internal static TitleDocument Create(TitleDocumentData documentData)
    {
        TitleDocumentValidator.Validate(documentData);

        return new TitleDocument
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentData.DocumentId,
            DocumentType = documentData.DocumentType,
            FileName = documentData.FileName,
            Prefix = documentData.Prefix,
            Set = documentData.Set,
            Notes = documentData.Notes,
            FilePath = documentData.FilePath,
            IsRequired = false, // TODO: Implement logic to get from configuration
            UploadedBy = documentData.UploadedBy,
            UploadedByName = documentData.UploadedByName,
            UploadedAt = documentData.UploadedAt
        };
    }

    internal void Update(TitleDocumentData documentData)
    {
        DocumentId = documentData.DocumentId;
        DocumentType = documentData.DocumentType;
        FileName = documentData.FileName;
        Prefix = documentData.Prefix;
        Set = documentData.Set;
        Notes = documentData.Notes;
        FilePath = documentData.FilePath;
        IsRequired = false;
        UploadedBy = documentData.UploadedBy;
        UploadedByName = documentData.UploadedByName;
        UploadedAt = documentData.UploadedAt;
    }

    internal void UpdateDraft(TitleDocumentData documentData)
    {
        DocumentId = documentData.DocumentId;
        DocumentType = documentData.DocumentType;
        FileName = documentData.FileName;
        Prefix = documentData.Prefix;
        Set = documentData.Set;
        Notes = documentData.Notes;
        FilePath = documentData.FilePath;
        IsRequired = false;
        UploadedBy = documentData.UploadedBy;
        UploadedByName = documentData.UploadedByName;
        UploadedAt = documentData.UploadedAt;
    }
}

public record TitleDocumentData
{
    public Guid? DocumentId { get; init; }
    public string? DocumentType { get; init; }
    public string? FileName { get; init; }
    public string? Prefix { get; init; }
    public int Set { get; init; }
    public string? Notes { get; init; }
    public string? FilePath { get; init; }
    public string? UploadedBy { get; init; }
    public string? UploadedByName { get; init; }
    public DateTime UploadedAt { get; init; }
};

public static class TitleDocumentValidator
{
    public static void Validate(TitleDocumentData documentData)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(documentData.DocumentType),
            "documentType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(documentData.UploadedBy),
            "uploadBy is null or contains only whitespace.");

        ruleCheck.ThrowIfInvalid();
    }
}