namespace Shared.Messaging.Events;

public record RequestResubmittedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; init; }

    /// <summary>
    /// When null: data-fix branch — consumer resumes the parent appraisal workflow at appraisal-initiation.
    /// When set: document-followup branch — consumer fulfills the followup's pending line items and resumes
    /// the followup child workflow at provide-additional-documents.
    /// </summary>
    public Guid? FollowupId { get; init; }

    public IReadOnlyList<ResubmittedFollowupItem> FollowupItems { get; init; } = [];
}

public record ResubmittedFollowupItem(string DocumentType, Guid DocumentId);
