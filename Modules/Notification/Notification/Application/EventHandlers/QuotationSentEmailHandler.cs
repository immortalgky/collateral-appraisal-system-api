using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email;
using Notification.Infrastructure.Email.Templates;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Consumes <see cref="QuotationSentEmailIntegrationEvent"/> from the Appraisal module
/// and sends a branded HTML email to the recipient(s) via <see cref="IEmailSender"/>.
/// </summary>
public sealed class QuotationSentEmailHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<QuotationSentEmailHandler> logger)
    : IConsumer<QuotationSentEmailIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationSentEmailIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

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

            var html = templateRenderer.QuotationSent(msg.Subject, msg.Content);

            var email = new EmailMessage(
                Subject: msg.Subject,
                HtmlBody: html,
                To: toAddresses.Count > 0 ? toAddresses : null,
                Cc: ccAddresses.Count > 0 ? ccAddresses : null,
                Bcc: bccAddresses.Count > 0 ? bccAddresses : null,
                Source: "QuotationSent",
                ReferenceId: msg.QuotationRequestId.ToString());

            await emailSender.SendAsync(email, context.CancellationToken);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error sending quotation email (MessageId={MessageId})", context.MessageId);
            throw;
        }
    }
}
