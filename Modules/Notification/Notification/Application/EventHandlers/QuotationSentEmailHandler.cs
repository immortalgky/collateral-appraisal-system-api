using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email;
using Notification.Infrastructure.Email.Attachments;
using Notification.Infrastructure.Email.Templates;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Consumes <see cref="QuotationSentEmailIntegrationEvent"/> from the Appraisal module
/// and sends a branded HTML email to the recipient(s) via <see cref="IEmailSender"/>.
/// </summary>
public sealed class QuotationSentEmailHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    EmailAttachmentAssembler attachmentAssembler,
    InboxGuard<NotificationDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider,
    ILogger<QuotationSentEmailHandler> logger)
    : IConsumer<QuotationSentEmailIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationSentEmailIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        // Taken right after the claim so ReleaseClaimAsync only removes our own row.
        var claimedBefore = dateTimeProvider.ApplicationNow;

        var msg = context.Message;

        try
        {
            var toAddresses = EmailRecipients.Parse(msg.To);
            var ccAddresses = EmailRecipients.Parse(msg.Cc);
            var bccAddresses = EmailRecipients.Parse(msg.Bcc);

            // To is optional (recipients may all be in Cc/Bcc); skip only if there are none at all.
            if (toAddresses.Count + ccAddresses.Count + bccAddresses.Count == 0)
            {
                logger.LogWarning(
                    "Skipping quotation email with no valid recipient (MessageId={MessageId})", context.MessageId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
                return;
            }

            var refs = msg.AttachmentRefs
                .Select(r => new EmailAttachmentRef(r.Type, r.Value))
                .ToList();

            var attachments = await attachmentAssembler.AssembleAsync(refs, context.CancellationToken);

            var html = templateRenderer.QuotationSent(msg.Subject, msg.Content);

            var email = new EmailMessage(
                Subject: msg.Subject,
                HtmlBody: html,
                To: toAddresses.Count > 0 ? toAddresses : null,
                Cc: ccAddresses.Count > 0 ? ccAddresses : null,
                Bcc: bccAddresses.Count > 0 ? bccAddresses : null,
                Attachments: attachments.Count > 0 ? attachments : null,
                Source: "QuotationSent",
                ReferenceId: msg.QuotationRequestId.ToString());

            await emailSender.SendAsync(email, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error sending quotation email (MessageId={MessageId})", context.MessageId);

            // Without this the 'Processing' claim makes every bus retry skip and ack the message,
            // so one SMTP hiccup would silently lose the email.
            try
            {
                await inboxGuard.ReleaseClaimAsync(
                    context.MessageId, GetType().Name, claimedBefore, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                logger.LogError(releaseEx,
                    "Could not release inbox claim for quotation email (MessageId={MessageId})",
                    context.MessageId);
            }

            throw;
        }

        // Outside the try on purpose: a failed mark after a successful send must not release the claim
        // and let a retry send the email twice.
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, CancellationToken.None);
    }
}
