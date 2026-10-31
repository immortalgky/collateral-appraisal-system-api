namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Appraisal module (outbox, in the same save as the assignment) when an assigned case's email to
/// the external company is ready: placeholders filled, recipients resolved. Consumed by the Notification module,
/// which sends it and answers with <see cref="AssignmentEmailSentIntegrationEvent"/>.
/// The From address is not carried: the bank gateway allows only the configured sender.
/// </summary>
public record AssignmentEmailRequestedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }

    /// <summary>The AssignmentEmails row; the send-log ReferenceId.</summary>
    public Guid EmailId { get; init; }

    public IReadOnlyList<string> To { get; init; } = [];
    public IReadOnlyList<string> Cc { get; init; } = [];
    public string Subject { get; init; } = default!;
    public string HtmlBody { get; init; } = default!;
    public IReadOnlyList<EmailAttachmentRefData> AttachmentRefs { get; init; } = [];

    /// <summary>The admin who assigned the case (bank code), for the failure notice.</summary>
    public string? AssignedBy { get; init; }
}

/// <summary>Published by the Notification module once the gateway accepted an assignment email.</summary>
public record AssignmentEmailSentIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid EmailId { get; init; }
}
