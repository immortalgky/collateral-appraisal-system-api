namespace Notification.Contracts.Email;

/// <summary>
/// Cross-module email sending port. Implemented by SmtpEmailSender in the Notification module.
/// The From address is always the configured sender (bank gateway constraint — callers cannot override it).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>
    /// Opens an SMTP connection (authenticating if a username is configured), issues a NOOP, then
    /// disconnects — a real round-trip to the gateway with no message sent. Throws when the host is
    /// unreachable, TLS negotiation fails, or auth is rejected. No-ops when <c>Mail:Enabled</c> is
    /// false. Used by the SMTP health check to probe connectivity.
    /// </summary>
    Task CheckConnectionAsync(CancellationToken ct = default);
}

/// <summary>
/// A single outbound email message. <see cref="To"/> is optional — the bank may hide recipients
/// by leaving it empty and addressing everyone via <see cref="Cc"/>/<see cref="Bcc"/>.
/// At least one of To/Cc/Bcc must be non-empty for the message to be sent.
/// </summary>
/// <param name="Source">
/// Optional send-log metadata — logical source name, e.g. "MeetingInvitation" or "QuotationSent".
/// Stored in <c>notification.EmailSendLogs</c> for ops investigation. Callers may leave it null.
/// </param>
/// <param name="ReferenceId">
/// Optional send-log metadata — business entity id that triggered the send, e.g. a MeetingId or
/// QuotationRequestId serialised as a string. Stored alongside <see cref="Source"/>.
/// </param>
public sealed record EmailMessage(
    string Subject,
    string HtmlBody,
    IReadOnlyList<string>? To = null,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    string? Source = null,
    string? ReferenceId = null);

/// <summary>An in-memory email attachment — resolved from a ref before sending.</summary>
public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);
