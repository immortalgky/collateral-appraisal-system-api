using Auth.Contracts.Users;
using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email.Templates;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Consumes <see cref="DocumentFollowupEmailIntegrationEvent"/> and emails the RM that additional
/// documents have been requested. RM email / display name and the acting user's signature name are
/// resolved here via <see cref="IUserLookupService"/>.
/// </summary>
public sealed class DocumentFollowupEmailHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    IUserLookupService userLookupService,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<DocumentFollowupEmailHandler> logger)
    : IConsumer<DocumentFollowupEmailIntegrationEvent>
{
    public async Task Consume(ConsumeContext<DocumentFollowupEmailIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;
        var ct = context.CancellationToken;

        try
        {
            var rm = string.IsNullOrWhiteSpace(msg.RmUsername)
                ? null
                : await userLookupService.GetRequestorAsync(msg.RmUsername, ct);

            if (rm?.Email is null || string.IsNullOrWhiteSpace(rm.Email))
            {
                logger.LogWarning(
                    "Skipping document-followup email: no RM email for RmUsername={RmUsername} (MessageId={MessageId})",
                    msg.RmUsername, context.MessageId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            var adminName = await ResolveNameAsync(msg.ActingUsername, ct);
            var subject = $"งานติดตามเอกสารของลูกค้าราย {msg.CustomerName ?? "-"}";
            var items = msg.Items
                .Select(i => new DocumentFollowupNoticeItem(i.DocumentName, i.Notes))
                .ToList();
            var model = new DocumentFollowupNoticeModel(
                rm.Name, msg.CustomerName, msg.AppraisalNumber, items, adminName);
            var html = templateRenderer.DocumentFollowupNotice(subject, model);

            await emailSender.SendAsync(new EmailMessage(
                Subject: subject,
                HtmlBody: html,
                To: [rm.Email],
                Source: "DocumentFollowup",
                ReferenceId: msg.FollowupId.ToString()), ct);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error sending document-followup email (MessageId={MessageId})", context.MessageId);
            throw;
        }
    }

    private async Task<string> ResolveNameAsync(string? username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username)) return string.Empty;
        var info = await userLookupService.GetRequestorAsync(username, ct);
        return info?.Name ?? username;
    }
}
