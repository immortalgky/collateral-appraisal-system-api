using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email.Attachments;
using Notification.Infrastructure.Email.Templates;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Sends the email to the company an appraisal was assigned to (#620), published by the Appraisal module with its
/// recipients and placeholders already resolved, then confirms it with <see cref="AssignmentEmailSentIntegrationEvent"/>.
/// </summary>
public sealed class AssignmentEmailRequestedHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    EmailAttachmentAssembler attachmentAssembler,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<AssignmentEmailRequestedHandler> logger)
    : IConsumer<AssignmentEmailRequestedIntegrationEvent>
{
    /// <summary>The send-log Source of these emails; ReferenceId is the AssignmentEmails row id.</summary>
    public const string Source = "AssignmentEmail";

    public Task Consume(ConsumeContext<AssignmentEmailRequestedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, async ct =>
        {
            var msg = context.Message;

            if (msg.To.Count + msg.Cc.Count == 0)
            {
                // ponytail: logged only; the row stays Pending until #624 reports failures.
                logger.LogWarning(
                    "Assignment email {EmailId} for appraisal {AppraisalId} has no recipient (the company has no email). Not sent",
                    msg.EmailId, msg.AppraisalId);
                return;
            }

            var attachments = await attachmentAssembler.AssembleAsync(
                msg.AttachmentRefs.Select(r => new EmailAttachmentRef(r.Type, r.Value)), ct);

            await emailSender.SendAsync(new EmailMessage(
                Subject: msg.Subject,
                HtmlBody: templateRenderer.AssignmentEmail(msg.Subject, msg.HtmlBody),
                To: msg.To.Count > 0 ? msg.To : null,
                Cc: msg.Cc.Count > 0 ? msg.Cc : null,
                Attachments: attachments.Count > 0 ? attachments : null,
                Source: Source,
                ReferenceId: msg.EmailId.ToString()), ct);

            // The gateway has the mail. Notification has no outbox, so a failed confirmation is only logged: throwing
            // would release the inbox claim and the retry would email the company a second time (decided 2026-11-01).
            try
            {
                await context.Publish(new AssignmentEmailSentIntegrationEvent
                {
                    AppraisalId = msg.AppraisalId,
                    EmailId = msg.EmailId
                }, CancellationToken.None); // not ct: a shutdown now must not turn into a resend either
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Assignment email {EmailId} for appraisal {AppraisalId} was sent but not confirmed; its row stays Pending",
                    msg.EmailId, msg.AppraisalId);
            }
        }, context.CancellationToken);
}
