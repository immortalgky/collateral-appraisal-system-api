namespace Document.UploadSessions.Model;

public class UploadSession : Aggregate<Guid>
{
    // Session Lifecycle
    public string Status { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Upload Statistics
    public int TotalDocuments { get; private set; }
    public long TotalSizeBytes { get; private set; }

    // Session Metadata
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }

    // Document Linkage
    private readonly List<Documents.Models.Document> _documents = [];
    public IReadOnlyList<Documents.Models.Document> Documents => _documents.AsReadOnly();

    private UploadSession()
    {
        // For EF Core
    }

    private UploadSession(
        DateTime expiresAt,
        string? userAgent,
        string? ipAddress)
    {
        Status = "InProgress";
        ExpiresAt = expiresAt;
        TotalDocuments = 0;
        TotalSizeBytes = 0;
        UserAgent = userAgent;
        IpAddress = ipAddress;
    }

    public static UploadSession Create(
        DateTime expiresAt,
        string? userAgent,
        string? ipAddress)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, DateTime.Now);

        return new UploadSession(
            expiresAt,
            userAgent,
            ipAddress
        );
    }

    public void IncrementDocumentCount(long fileSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Status, "Completed");
        ArgumentOutOfRangeException.ThrowIfLessThan(ExpiresAt, DateTime.Now);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileSizeBytes);

        TotalDocuments++;
        TotalSizeBytes += fileSizeBytes;
    }

    public void Complete(DateTime completedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Status, "Completed");

        Status = "Completed";
        CompletedAt = completedAt;
    }
}