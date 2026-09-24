using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email;
using Notification.Infrastructure.Email.Attachments;
using Notification.Infrastructure.Email.Templates;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Consumes <see cref="MeetingInvitationEmailIntegrationEvent"/> from the Workflow module.
/// Resolves typed attachment refs (document + report), renders the invitation template,
/// and sends the email via <see cref="IEmailSender"/>.
/// </summary>
public sealed class MeetingInvitationEmailHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    EmailAttachmentAssembler attachmentAssembler,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<MeetingInvitationEmailHandler> logger)
    : IConsumer<MeetingInvitationEmailIntegrationEvent>
{
    public Task Consume(ConsumeContext<MeetingInvitationEmailIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, async ct =>
        {
            var msg = context.Message;

            // Guard before the expensive attachment assembly (report PDF + document reads).
            // To is optional (recipients may all be in Cc/Bcc); skip only if there are none at all.
            var toAddresses = EmailRecipients.Parse(msg.To);
            var ccAddresses = EmailRecipients.Parse(msg.Cc);
            var bccAddresses = EmailRecipients.Parse(msg.Bcc);

            if (toAddresses.Count + ccAddresses.Count + bccAddresses.Count == 0)
            {
                logger.LogWarning(
                    "Skipping meeting invitation email with no valid recipient (MessageId={MessageId})", context.MessageId);
                return;
            }

            // Map transport refs → Notification.Contracts refs
            var refs = msg.AttachmentRefs
                .Select(r => new EmailAttachmentRef(r.Type, r.Value))
                .ToList();

            var attachments = await attachmentAssembler.AssembleAsync(refs, ct);

            var html = templateRenderer.MeetingInvitation(msg.Subject, msg.Content);

            var email = new EmailMessage(
                Subject: msg.Subject,
                HtmlBody: html,
                To: toAddresses.Count > 0 ? toAddresses : null,
                Cc: ccAddresses.Count > 0 ? ccAddresses : null,
                Bcc: bccAddresses.Count > 0 ? bccAddresses : null,
                Attachments: attachments.Count > 0 ? attachments : null,
                Source: "MeetingInvitation",
                ReferenceId: msg.MeetingId.ToString());

            await emailSender.SendAsync(email, ct);
        }, context.CancellationToken);
}
