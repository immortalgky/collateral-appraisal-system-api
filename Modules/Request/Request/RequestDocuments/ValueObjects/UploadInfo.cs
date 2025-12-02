using System;

namespace Request.RequestDocuments.ValueObjects;

public class UploadInfo : ValueObject
{
    public long? UploadedBy { get; }
    public string? UploadedByName { get; }
    public DateTime? UploadedAt { get; }

    private UploadInfo(long? uploadedBy, string? uploadedByName, DateTime? uploadedAt)
    {
        UploadedBy = uploadedBy;
        UploadedByName = uploadedByName;
        UploadedAt = uploadedAt;
    }

    public static UploadInfo Create(long? uploadedBy, string? uploadedByName, DateTime? uploadAt)
    {
        return new UploadInfo(uploadedBy, uploadedByName, uploadAt);
    }
}
