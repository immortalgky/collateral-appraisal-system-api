namespace Workflow.Meetings.Domain;

/// <summary>
/// An entity owned by the <see cref="Meeting"/> aggregate that links a Document
/// (from the Document module) to a meeting. Tracks whether the document was generated
/// server-side (Generated) or uploaded by the user (Uploaded).
/// Inherits <c>CreatedAt</c> and <c>CreatedBy</c> from <see cref="Entity{T}"/>.
/// </summary>
public class MeetingDocument : Entity<Guid>
{
    public Guid MeetingId { get; private set; }
    public Guid DocumentId { get; private set; }

    /// <summary>"Invitation" | "Minute" | "Upload"</summary>
    public string DocumentType { get; private set; } = default!;

    public string FileName { get; private set; } = default!;

    /// <summary>"Generated" | "Uploaded"</summary>
    public string Source { get; private set; } = default!;

    private MeetingDocument() { }

    internal static MeetingDocument Create(Guid meetingId, MeetingDocumentData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data.DocumentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CreatedBy);

        return new MeetingDocument
        {
            Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            DocumentId = data.DocumentId,
            DocumentType = data.DocumentType,
            FileName = data.FileName,
            Source = data.Source,
            // Base Entity<> fields — set explicitly (AuditableEntityInterceptor also sets them on SaveChanges,
            // but we set them here for domain accuracy when returning the dto before SaveChanges runs).
            CreatedBy = data.CreatedBy,
            CreatedAt = data.CreatedAt
        };
    }
}

/// <summary>Data transfer object used by <see cref="Meeting.AddDocument"/>.</summary>
public record MeetingDocumentData(
    Guid DocumentId,
    string DocumentType,
    string FileName,
    string Source,
    string CreatedBy,
    DateTime CreatedAt);
