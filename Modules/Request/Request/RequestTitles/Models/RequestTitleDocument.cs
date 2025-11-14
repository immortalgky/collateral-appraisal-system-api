namespace Request.RequestTitles.Models;

public class RequestTitleDocument : Aggregate<Guid>
{
    public Guid TitleId { get; private set; }
    public Guid DocumentId { get; private set; }

    public string DocumentType { get; private set; } = default!;
    public bool IsRequired { get; private set; } = false;

    public string DocumentDescription { get; private set; } = default!;

    public string UploadedBy { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTime UploadedAt { get; private set; } = DateTime.UtcNow;
    
    private RequestTitleDocument()
    {
        // EF Core
    }

    public static RequestTitleDocument Create(Guid titleId, Guid documentId, string documentType, string documentDescription, bool isRequired, string uploadedBy, string uploadedByName)
    {
        return new RequestTitleDocument()
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            DocumentId = documentId,
            DocumentType = documentType,
            IsRequired = isRequired,
            DocumentDescription = documentDescription,
            UploadedBy = uploadedBy,
            UploadedByName = uploadedByName,
            UploadedAt = DateTime.UtcNow
        };
    }

    public bool Update(string documentType, string documentDescription, bool isRequired, string uploadedBy,
        string uploadedByName)
    {
        DocumentType = documentType;
        DocumentDescription = documentDescription;
        IsRequired = isRequired;
        UploadedBy = uploadedBy;
        UploadedByName = uploadedByName;
        UploadedAt = DateTime.UtcNow;
        
        return true;
    }
}