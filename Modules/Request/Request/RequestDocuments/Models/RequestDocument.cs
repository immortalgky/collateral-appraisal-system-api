namespace Request.RequestDocuments.Models;

public class RequestDocument : Aggregate<Guid>
{
    public Guid RequestId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string? FileName { get; private set; }
    public string? Prefix { get; private set; }
    public short? Set { get; private set; }
    public string? FilePath { get; private set; }
    public bool DocumentFollowUp { get; private set; }
    public DocumentClassification DocumentClassification { get; private set; }
    public UploadInfo UploadInfo { get; private set; }
    public string? DocumentDescription { get; private set; }

    public RequestDocument()
    {
        //EF Core
    }

    private RequestDocument(
        Guid requestId,
        Guid? documentId,
        string? fileName,
        string? prefix,
        short? set,
        string? filePath,
        bool documentFollowUp,
        DocumentClassification documentClassification,
        string documentDescription,
        UploadInfo uploadInfo
    )
    {
        RequestId = requestId;
        DocumentId = documentId;
        FileName = fileName;
        Prefix = prefix;
        Set = set;
        FilePath = filePath;
        DocumentFollowUp = documentFollowUp;
        DocumentClassification = documentClassification;
        DocumentDescription = documentDescription;
        UploadInfo = uploadInfo;
    }

    public static RequestDocument Create(
        Guid requestId,
        Guid? documentId,
        string? fileName,
        string? prefix,
        short? set,
        string? filePath,
        bool documentFollowUp,
        DocumentClassification documentClassification,
        string? documentDescription,
        UploadInfo uploadInfo
    )
    {
        var requestDocument = new RequestDocument(requestId, documentId, fileName, prefix, set, filePath,
            documentFollowUp, documentClassification, documentDescription,
            uploadInfo);

        return requestDocument;
    }

    public void UpdateRequestDocument(
        Guid? documentId,
        string? fileName,
        string? prefix,
        short? set,
        string? filePath,
        bool documentFollowUp,
        DocumentClassification documentClassification,
        string? documentDescription,
        UploadInfo uploadInfo
    )
    {
        DocumentId = documentId;
        FileName = fileName;
        Prefix = prefix;
        Set = set;
        FilePath = filePath;
        DocumentFollowUp = documentFollowUp;
        DocumentClassification = documentClassification;
        DocumentDescription = documentDescription;
        UploadInfo = uploadInfo;
    }
}