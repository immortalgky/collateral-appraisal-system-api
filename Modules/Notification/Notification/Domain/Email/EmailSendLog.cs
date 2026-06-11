namespace Notification.Domain.Email;

/// <summary>
/// Durable record of every email send attempt — regardless of outcome.
/// Written by <c>SmtpEmailSender</c> at each of its four exit paths:
/// Sent, Failed, SkippedDisabled, SkippedNoRecipient.
/// </summary>
public class EmailSendLog
{
    public Guid Id { get; private set; }

    /// <summary>Logical source, e.g. "MeetingInvitation" or "QuotationSent". Null when called ad-hoc.</summary>
    public string? Source { get; private set; }

    /// <summary>Business entity id that triggered the send, e.g. a MeetingId or QuotationRequestId.</summary>
    public string? ReferenceId { get; private set; }

    /// <summary>Comma-joined To addresses from the message (may be null when all recipients are Cc/Bcc).</summary>
    public string? ToAddresses { get; private set; }

    /// <summary>Comma-joined Cc addresses.</summary>
    public string? CcAddresses { get; private set; }

    /// <summary>Comma-joined Bcc addresses.</summary>
    public string? BccAddresses { get; private set; }

    /// <summary>The configured From address used for this send.</summary>
    public string FromAddress { get; private set; } = default!;

    public string Subject { get; private set; } = default!;

    /// <summary>Total recipients across To + Cc + Bcc at the point of the send attempt.</summary>
    public int RecipientCount { get; private set; }

    /// <summary>Number of attachments in the message (0 when none).</summary>
    public int AttachmentCount { get; private set; }

    /// <summary>Outcome: "Sent" | "Failed" | "SkippedDisabled" | "SkippedNoRecipient".</summary>
    public string Status { get; private set; } = default!;

    /// <summary>Exception type + message when Status = "Failed".</summary>
    public string? Error { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private EmailSendLog() { }

    public static EmailSendLog Create(
        string status,
        string fromAddress,
        string subject,
        int recipientCount,
        int attachmentCount,
        DateTime createdAt,
        string? source = null,
        string? referenceId = null,
        string? toAddresses = null,
        string? ccAddresses = null,
        string? bccAddresses = null,
        string? error = null)
    {
        return new EmailSendLog
        {
            Id = Guid.CreateVersion7(),
            Status = status,
            FromAddress = fromAddress,
            Subject = subject,
            RecipientCount = recipientCount,
            AttachmentCount = attachmentCount,
            CreatedAt = createdAt,
            Source = source,
            ReferenceId = referenceId,
            ToAddresses = toAddresses,
            CcAddresses = ccAddresses,
            BccAddresses = bccAddresses,
            Error = error
        };
    }
}
