namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module (via outbox in SendInvitationCommandHandler) when a
/// meeting invitation is dispatched. Consumed by the Notification module which resolves
/// attachments, renders an HTML template, and delivers the email via SMTP.
/// </summary>
public record MeetingInvitationEmailIntegrationEvent : IntegrationEvent
{
    /// <summary>The meeting that triggered this send — used as the send-log ReferenceId.</summary>
    public Guid MeetingId { get; init; }

    public string? To { get; init; }
    public string? Cc { get; init; }
    public string? Bcc { get; init; }
    public string Subject { get; init; } = default!;
    public string? Content { get; init; }

    /// <summary>
    /// Typed attachment references resolved at send time. Each entry carries a
    /// discriminator <c>Type</c> ("document" | "report") and a string <c>Value</c>
    /// (document Guid, or "reportKey:entityId").
    /// <para>
    /// A parallel record is used here rather than referencing Notification.Contracts
    /// directly, avoiding a dependency inversion (Notification module already depends
    /// on Shared.Messaging — having Shared.Messaging depend on Notification.Contracts
    /// would create a cycle). The consumer maps these to <c>EmailAttachmentRef</c>.
    /// </para>
    /// </summary>
    public IReadOnlyList<EmailAttachmentRefData> AttachmentRefs { get; init; } = [];
}

/// <summary>Transport-level attachment reference — mirrors <c>Notification.Contracts.Email.EmailAttachmentRef</c>.</summary>
public sealed record EmailAttachmentRefData(string Type, string Value);
