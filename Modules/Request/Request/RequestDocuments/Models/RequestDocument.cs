using System;
using System.Reflection.Metadata;
using Request.RequestDocuments.ValueObjects;

namespace Request.RequestDocuments.Models;

public class RequestDocument : Aggregate<Guid>
{
    public Guid RequestId { get; private set; }
    public Guid DocumentId { get; private set; }
    public DocumentClassification DocumentClassification { get; private set; }
    public UploadInfo UploadInfo { get; private set; }
    public string? DocumentDescription { get; private set; }


    public RequestDocument()
    {
        //EF Core
    }

    private RequestDocument(
        Guid requestId,
        Guid documentId,
        DocumentClassification documentClassification,
        string documentDescription,
        UploadInfo uploadInfo
    )
    {
        RequestId = requestId;
        DocumentId = documentId;
        DocumentClassification = documentClassification;
        DocumentDescription = documentDescription;
        UploadInfo = uploadInfo;
    }

    public static RequestDocument Create(
        Guid requestId,
        Guid documentId,
        DocumentClassification documentClassification,
        string documentDescription,
        UploadInfo uploadInfo
    )
    {
        var requestDocument = new RequestDocument(requestId, documentId, documentClassification, documentDescription,
            uploadInfo);

        return requestDocument;
    }
}
