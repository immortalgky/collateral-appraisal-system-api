namespace Request.Contracts.RequestDocuments;

/// <summary>
/// Allows other modules (e.g. Workflow / DocumentFollowups) to attach already-uploaded
/// documents to a Request or one of its Titles without taking a hard reference on the
/// Request domain. Implementations MUST raise the domain events that trigger the
/// `DocumentLinkedIntegrationEventV2` outbox publication, so downstream consumers
/// (e.g. followup auto-fulfill) stay wired.
/// </summary>
public interface IRequestDocumentAttacher
{
    Task AttachToRequestAsync(
        Guid requestId,
        AttachedDocumentInput input,
        CancellationToken cancellationToken = default);

    Task AttachToTitleAsync(
        Guid requestId,
        Guid titleId,
        AttachedDocumentInput input,
        CancellationToken cancellationToken = default);
}

public record AttachedDocumentInput(
    Guid DocumentId,
    string DocumentType,
    string FileName,
    string? UploadedBy = null,
    string? UploadedByName = null);
