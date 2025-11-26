namespace Request.RequestTitles.Models;

public class RequestTitleDocument : Aggregate<Guid>
{
    public Guid TitleId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string? DocumentType { get; private set; }
    public string? Filename { get; private set; }
    public string? Prefix { get; private set; }
    public int Set { get; private set; }
    public string? DocumentDescription { get; private set; }
    public string? FilePath { get; private set; }
    public string? CreatedWorkstation { get; private set; }
    public bool IsRequired { get; private set; } = false;
    public string? UploadedBy { get; private set; } = default!;
    public string? UploadedByName { get; private set; } = default!;
    public DateTime UploadedAt { get; private set; } = DateTime.UtcNow;
    
    private RequestTitleDocument()
    {
        // EF Core
    }
    

    public static RequestTitleDocument Create(RequestTitleDocumentData requestDocumentData)
    {
        return new RequestTitleDocument()
        {
            Id = Guid.NewGuid(),
            TitleId = requestDocumentData.TitleId,
            DocumentId = requestDocumentData.DocumentId,
            DocumentType = requestDocumentData.DocumentType,
            Filename = requestDocumentData.Filename,
            Prefix = requestDocumentData.Prefix,
            Set = requestDocumentData.Set,
            DocumentDescription = requestDocumentData.DocumentDescription,
            CreatedWorkstation = requestDocumentData.CreatedWorkstation,
            FilePath = requestDocumentData.FilePath,
            IsRequired = requestDocumentData.IsRequired,
            UploadedBy = requestDocumentData.UploadedBy,
            UploadedByName = requestDocumentData.UploadedByName,
            UploadedAt = DateTime.UtcNow
        };
    }
    
    public void Update(RequestTitleDocumentData requestDocumentData)
    {
        DocumentId = requestDocumentData.DocumentId;
        DocumentType = requestDocumentData.DocumentType;
        Filename = requestDocumentData.Filename;
        Prefix = requestDocumentData.Prefix;
        Set = requestDocumentData.Set;
        DocumentDescription = requestDocumentData.DocumentDescription;
        FilePath = requestDocumentData.FilePath;
        CreatedWorkstation = requestDocumentData.CreatedWorkstation;
        IsRequired = requestDocumentData.IsRequired;
        UploadedBy = requestDocumentData.UploadedBy;
        UploadedByName = requestDocumentData.UploadedByName;
        UploadedAt = DateTime.UtcNow;
    }

    public void UpdateDraft(RequestTitleDocumentData requestDocumentData)
    {
        DocumentId = requestDocumentData.DocumentId;
        DocumentType = requestDocumentData.DocumentType;
        Filename = requestDocumentData.Filename;
        Prefix = requestDocumentData.Prefix;
        Set = requestDocumentData.Set;
        DocumentDescription = requestDocumentData.DocumentDescription;
        CreatedWorkstation = requestDocumentData.CreatedWorkstation;
        FilePath = requestDocumentData.FilePath;
        IsRequired = requestDocumentData.IsRequired;
        UploadedBy = requestDocumentData.UploadedBy;
        UploadedByName = requestDocumentData.UploadedByName;
        UploadedAt = DateTime.UtcNow;
    }
}

public record RequestTitleDocumentData
{
    public Guid TitleId { get; init; }
    public Guid? DocumentId { get; init; }
    public string? DocumentType { get; init; }
    public string? Filename { get; init; }
    public string? Prefix { get; init; }
    public int Set { get; init; }
    public string? DocumentDescription { get; init; }
    public string? FilePath { get; init; }
    public string? CreatedWorkstation { get; init; }
    public bool IsRequired { get; init; }
    public string? UploadedBy { get; init; }
    public string? UploadedByName { get; init; }
};

public static class RequestTitleDocumentValidator
{
    public static void Validate(RequestTitleDocumentData requestTitleDocumentData)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(String.IsNullOrWhiteSpace(requestTitleDocumentData.DocumentType), "documentType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(String.IsNullOrWhiteSpace(requestTitleDocumentData.UploadedBy), "uploadBy is null or contains only whitespace.");
        ruleCheck.AddErrorIf(String.IsNullOrWhiteSpace(requestTitleDocumentData.UploadedByName), "uploadByName is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}