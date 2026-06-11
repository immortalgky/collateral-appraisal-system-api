using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Notification.Contracts.Email;
using Notification.Domain.Email;
using Notification.Infrastructure.Email.Templates;
using Shared.Time;

namespace Notification.Infrastructure.Email;

/// <summary>
/// MailKit-based SMTP adapter for <see cref="IEmailSender"/>.
/// <para>
/// Key behaviours:
/// <list type="bullet">
///   <item>From address is always <see cref="MailConfiguration.FromAddress"/> — callers cannot override it (bank gateway rule).</item>
///   <item>STARTTLS negotiated when <see cref="MailConfiguration.UseStartTls"/> is true; plain connection otherwise.</item>
///   <item>Authentication performed only when <see cref="MailConfiguration.Username"/> is non-empty (smtp4dev / Mailpit are anonymous).</item>
///   <item>When <see cref="MailConfiguration.Enabled"/> is false the method logs and returns immediately — no connection attempted.</item>
///   <item>Every send attempt (including skips and failures) is written to <c>notification.EmailSendLogs</c> via <see cref="IEmailSendLogWriter"/> (best-effort — a log write failure never masks the real result).</item>
/// </list>
/// </para>
/// </summary>
internal sealed class SmtpEmailSender(
    IOptions<MailConfiguration> options,
    IDateTimeProvider dateTimeProvider,
    IEmailSendLogWriter sendLogWriter,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    /// <summary>Status constants for <c>notification.EmailSendLogs</c>.</summary>
    private static class SendStatus
    {
        public const string Sent = "Sent";
        public const string Failed = "Failed";
        public const string SkippedDisabled = "SkippedDisabled";
        public const string SkippedNoRecipient = "SkippedNoRecipient";
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var config = options.Value;

        // One place to record a send-log row (best-effort). Closes over message/config.
        Task WriteLogAsync(string status, int recipientCount, int attachmentCount, string? error = null) =>
            TryWriteLogAsync(EmailSendLog.Create(
                status: status,
                fromAddress: config.FromAddress ?? string.Empty,
                subject: message.Subject,
                recipientCount: recipientCount,
                attachmentCount: attachmentCount,
                createdAt: dateTimeProvider.ApplicationNow,
                source: message.Source,
                referenceId: message.ReferenceId,
                toAddresses: JoinAddresses(message.To),
                ccAddresses: JoinAddresses(message.Cc),
                bccAddresses: JoinAddresses(message.Bcc),
                error: error),
                ct);

        if (!config.Enabled)
        {
            logger.LogInformation(
                "Mail is disabled (Mail:Enabled=false). Skipping email subject '{Subject}'",
                message.Subject);
            await WriteLogAsync(SendStatus.SkippedDisabled, recipientCount: 0, attachmentCount: 0);
            return;
        }

        var recipientCount = 0;
        var attachmentCount = 0;
        try
        {
            // Build the MimeMessage INSIDE the try: MailboxAddress.Parse / ContentType.Parse can
            // throw on a malformed recipient or MIME type, and must be logged as Failed (not leaked
            // unlogged) — the FE only checks the address string contains '@', so bad input is reachable.
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(config.FromDisplayName ?? config.FromAddress, config.FromAddress));

            AddRecipients(mime.To, message.To);
            AddRecipients(mime.Cc, message.Cc);
            AddRecipients(mime.Bcc, message.Bcc);

            // To is optional (recipients may all be in Bcc), but at least one recipient is required.
            recipientCount = mime.To.Count + mime.Cc.Count + mime.Bcc.Count;
            if (recipientCount == 0)
            {
                logger.LogWarning(
                    "No recipients (To/Cc/Bcc all empty). Skipping email subject '{Subject}'", message.Subject);
                await WriteLogAsync(SendStatus.SkippedNoRecipient,
                    recipientCount: 0, attachmentCount: message.Attachments?.Count ?? 0);
                return;
            }

            mime.Subject = message.Subject;

            var builder = new BodyBuilder { HtmlBody = message.HtmlBody };

            // Inline brand logo: attach as a linked resource only when the HTML references its CID.
            // Keeps the logo embedded (no external fetch, survives image-blocking) without inflating
            // the user-facing attachment count.
            if (message.HtmlBody.Contains("cid:" + EmailBranding.LogoContentId, StringComparison.Ordinal))
            {
                var logo = builder.LinkedResources.Add(
                    EmailBranding.LogoFileName, EmailBranding.LogoBytes,
                    ContentType.Parse(EmailBranding.LogoContentType));
                logo.ContentId = EmailBranding.LogoContentId;
                logo.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
            }

            if (message.Attachments is not null)
            {
                foreach (var att in message.Attachments)
                {
                    builder.Attachments.Add(att.FileName, att.Content,
                        ContentType.Parse(att.ContentType));
                    attachmentCount++;
                }
            }

            mime.Body = builder.ToMessageBody();

            // Connect and send
            var secureOption = config.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

            using var client = new SmtpClient();
            await client.ConnectAsync(config.Host, config.Port, secureOption, ct);

            if (!string.IsNullOrEmpty(config.Username))
                await client.AuthenticateAsync(config.Username, config.Password, ct);

            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(quit: true, ct);

            logger.LogInformation(
                "Email sent ({RecipientCount} recipient(s)) subject '{Subject}'",
                recipientCount, message.Subject);
            await WriteLogAsync(SendStatus.Sent, recipientCount, attachmentCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var errorMessage = $"{ex.GetType().Name}: {ex.Message}";

            logger.LogError(ex,
                "Failed to send email subject '{Subject}': {Error}", message.Subject, errorMessage);
            await WriteLogAsync(SendStatus.Failed, recipientCount, attachmentCount, errorMessage);

            throw; // preserve retry semantics
        }
    }

    /// <summary>
    /// Best-effort log write — swallows any exception so a persistence failure
    /// can never mask the real email send result or throw to the caller.
    /// </summary>
    private async Task TryWriteLogAsync(EmailSendLog log, CancellationToken ct)
    {
        try
        {
            await sendLogWriter.WriteAsync(log, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write EmailSendLog (non-fatal). Status={Status} Subject='{Subject}'",
                log.Status, log.Subject);
        }
    }

    private static void AddRecipients(InternetAddressList target, IReadOnlyList<string>? addresses)
    {
        if (addresses is null) return;
        foreach (var addr in addresses.Where(a => !string.IsNullOrWhiteSpace(a)))
            target.Add(MailboxAddress.Parse(addr.Trim()));
    }

    private static string? JoinAddresses(IReadOnlyList<string>? addresses)
    {
        if (addresses is not { Count: > 0 }) return null;
        var joined = string.Join(", ", addresses.Where(a => !string.IsNullOrWhiteSpace(a)));
        if (joined.Length == 0) return null;
        // Cap to the EmailSendLogs column width so a pathological recipient list
        // can't make the (best-effort) log write throw a truncation error.
        return joined.Length > 1000 ? joined[..1000] : joined;
    }
}
