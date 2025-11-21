namespace Request.RequestTitles.Models;

public class RequestTitleDocument : Aggregate<Guid>
{
    public Guid TitleId { get; private set; }
    public Guid DocumentId { get; private set; }

    public string DocumentType { get; private set; } = default!;
    public bool IsRequired { get; private set; } = false;

    public string? DocumentDescription { get; private set; }

    public string UploadedBy { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTime UploadedAt { get; private set; } = DateTime.UtcNow;
    
    private RequestTitleDocument()
    {
        // EF Core
    }
    

    public static RequestTitleDocument Create(RequestTitleDocumentData requestTitleDocument)
    {
        return new RequestTitleDocument()
        {
            Id = Guid.NewGuid(),
            TitleId = requestTitleDocument.TitleId,
            DocumentId = requestTitleDocument.DocumentId,
            DocumentType = requestTitleDocument.DocumentType,
            IsRequired = requestTitleDocument.IsRequired,
            DocumentDescription = requestTitleDocument.DocumentDescription,
            UploadedBy = requestTitleDocument.UploadedBy,
            UploadedByName = requestTitleDocument.UploadedByName,
            UploadedAt = DateTime.UtcNow
        };
    }
    
    public void Update(RequestTitleDocumentData requestDocumentData)
    {
        RequestTitleDocumentValidator.Validate(requestDocumentData);
        
        DocumentType = requestDocumentData.DocumentType;
        DocumentDescription = requestDocumentData.DocumentDescription;
        IsRequired = requestDocumentData.IsRequired;
        UploadedBy = requestDocumentData.UploadedBy;
        UploadedByName = requestDocumentData.UploadedByName;
        UploadedAt = DateTime.UtcNow;
    }

    public void UpdateDraft(RequestTitleDocumentData requestDocumentData)
    {
        DocumentType = requestDocumentData.DocumentType;
        DocumentDescription = requestDocumentData.DocumentDescription;
        IsRequired = requestDocumentData.IsRequired;
        UploadedBy = requestDocumentData.UploadedBy;
        UploadedByName = requestDocumentData.UploadedByName;
        UploadedAt = DateTime.UtcNow;
    }
}

public record RequestTitleDocumentData(
    Guid TitleId,
    Guid DocumentId,
    string DocumentType,
    bool IsRequired,
    string DocumentDescription,
    string UploadedBy,
    string UploadedByName
    );

public static class RequestTitleDocumentValidator
{
    public static void Validate(RequestTitleDocumentData requestTitleDocumentData)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(!String.IsNullOrWhiteSpace(requestTitleDocumentData.DocumentType), "documentType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(!String.IsNullOrWhiteSpace(requestTitleDocumentData.UploadedBy), "uploadBy is null or contains only whitespace.");
        ruleCheck.AddErrorIf(!String.IsNullOrWhiteSpace(requestTitleDocumentData.UploadedByName), "uploadByName is null or contains only whitespace.");
    }
}