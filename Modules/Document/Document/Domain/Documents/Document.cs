using Document.Domain.UploadSessions.Model;

namespace Document.Domain.Documents.Models;

public class Document : Aggregate<Guid>
{
    // Upload Session Tracking
    public Guid UploadSessionId { get; private set; } = default!;

    // Document Classification
    public string DocumentType { get; private set; } = default!;
    public string DocumentCategory { get; private set; } = default!;

    // File Information
    public string FileName { get; private set; } = default!;
    public string FileExtension { get; private set; } = default!;
    public long FileSizeBytes { get; private set; }
    public string MimeType { get; private set; } = default!;

    // Storage (Local File System)
    public string StoragePath { get; private set; } = default!;
    public string StorageUrl { get; private set; } = default!;

    // Upload Information
    public string UploadedBy { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTime UploadedAt { get; private set; }

    // Reference Counting (for orphan detection)
    public int ReferenceCount { get; private set; }
    public DateTime? LastLinkedAt { get; private set; }
    public DateTime? LastUnlinkedAt { get; private set; }

    // Orphan Detection
    public bool IsOrphaned { get; private set; }
    public string? OrphanedReason { get; private set; }

    // Access Control
    public string AccessLevel { get; private set; } = default!;

    // Status
    public bool IsActive { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public string? ArchivedBy { get; private set; }
    public string? ArchivedByName { get; private set; }

    // Metadata
    public string? Description { get; private set; }
    public string? Tags { get; private set; }
    public string? CustomMetadata { get; private set; }

    // File Integrity
    public string? Checksum { get; private set; }
    public string? ChecksumAlgorithm { get; private set; }

    // Soft Delete
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    private Document()
    {
        // For EF Core
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private Document(
        Guid id,
        Guid uploadSessionId,
        string documentType,
        string documentCategory,
        string fileName,
        string fileExtension,
        long fileSizeBytes,
        string mimeType,
        string storagePath,
        string storageUrl,
        string uploadedBy,
        string uploadedByName,
        DateTime uploadedAt,
        string? description,
        string? tags,
        string? customMetadata,
        string? checksum,
        string? checksumAlgorithm
    )
    {
        Id = id;
        UploadSessionId = uploadSessionId;
        DocumentType = documentType;
        DocumentCategory = documentCategory;
        FileName = fileName;
        FileExtension = fileExtension;
        FileSizeBytes = fileSizeBytes;
        MimeType = mimeType;
        StoragePath = storagePath;
        StorageUrl = storageUrl;
        UploadedBy = uploadedBy;
        UploadedByName = uploadedByName;
        UploadedAt = uploadedAt;
        ReferenceCount = 0;
        IsOrphaned = false;
        AccessLevel = "Public";
        IsActive = true;
        IsArchived = false;
        Description = description;
        Tags = tags;
        CustomMetadata = customMetadata;
        Checksum = checksum;
        ChecksumAlgorithm = checksumAlgorithm;
        IsDeleted = false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Document Create(
        Guid id,
        Guid uploadSessionId,
        string documentType,
        string documentCategory,
        string fileName,
        string fileExtension,
        long fileSizeBytes,
        string mimeType,
        string storagePath,
        string storageUrl,
        string uploadedBy,
        string uploadedByName,
        DateTime uploadedAt,
        string? description,
        string? tags,
        string? customMetadata,
        string? checksum,
        string? checksumAlgorithm
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        // ArgumentException.ThrowIfNullOrWhiteSpace(documentCategory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileSizeBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedByName);

        return new Document(
            id,
            uploadSessionId,
            documentType,
            documentCategory,
            fileName,
            fileExtension,
            fileSizeBytes,
            mimeType,
            storagePath,
            storageUrl,
            uploadedBy,
            uploadedByName,
            uploadedAt,
            description,
            tags,
            customMetadata,
            checksum,
            checksumAlgorithm
        );
    }

    public void Link(DateTime linkedAt)
    {
        ReferenceCount++;
        LastLinkedAt = linkedAt;
        IsOrphaned = false;
        OrphanedReason = null;
    }

    public void Unlink(DateTime unlinkedAt)
    {
        ReferenceCount--;
        LastUnlinkedAt = unlinkedAt;

        if (ReferenceCount == 0)
        {
            IsOrphaned = true;
            OrphanedReason = "No active references";
        }
    }

    public void UpdateStoragePath(string newPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        StoragePath = newPath;
    }
}