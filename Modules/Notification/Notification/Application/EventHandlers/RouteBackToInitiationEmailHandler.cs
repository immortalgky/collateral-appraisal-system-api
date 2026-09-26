using Auth.Contracts.Users;
using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email.Templates;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Consumes <see cref="RouteBackToInitiationEmailIntegrationEvent"/> and emails the RM that the
/// appraisal has been routed back to appraisal-initiation to fix collateral data. RM email /
/// display name and the acting user's signature name are resolved here via <see cref="IUserLookupService"/>.
/// </summary>
public sealed class RouteBackToInitiationEmailHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    IUserLookupService userLookupService,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<RouteBackToInitiationEmailHandler> logger)
    : IConsumer<RouteBackToInitiationEmailIntegrationEvent>
{
    public Task Consume(ConsumeContext<RouteBackToInitiationEmailIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, async ct =>
        {
            var msg = context.Message;

            var rm = string.IsNullOrWhiteSpace(msg.RmUsername)
                ? null
                : await userLookupService.GetRequestorAsync(msg.RmUsername, ct);

            if (rm?.Email is null || string.IsNullOrWhiteSpace(rm.Email))
            {
                logger.LogWarning(
                    "Skipping route-back email: no RM email for RmUsername={RmUsername} (MessageId={MessageId})",
                    msg.RmUsername, context.MessageId);
                return;
            }

            // Sender (the user who made the route-back decision) — full name + phone for the footer contact.
            var sender = string.IsNullOrWhiteSpace(msg.ActingUsername)
                ? null
                : await userLookupService.GetRequestorAsync(msg.ActingUsername, ct);
            var senderName = sender?.Name ?? msg.ActingUsername ?? string.Empty;

            var subject = $"ขอรายละเอียดข้อมูลเพิ่มเติม ลูกค้าราย {msg.CustomerName ?? "-"}";
            var model = new RouteBackNoticeModel(
                rm.Name, msg.CustomerName, msg.AppraisalNumber, msg.Remark, senderName, sender?.ContactNo);
            var html = templateRenderer.RouteBackNotice(subject, model);

            await emailSender.SendAsync(new EmailMessage(
                Subject: subject,
                HtmlBody: html,
                To: [rm.Email],
                Source: "RouteBackToInitiation",
                ReferenceId: msg.AppraisalId.ToString()), ct);
        }, context.CancellationToken);
}
