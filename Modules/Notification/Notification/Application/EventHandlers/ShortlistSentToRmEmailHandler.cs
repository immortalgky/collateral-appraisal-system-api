using Auth.Contracts.Users;
using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email;
using Notification.Infrastructure.Email.Templates;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Consumes <see cref="ShortlistSentToRmEmailIntegrationEvent"/> and emails the RM a
/// fee-comparison table so they can pick the winning appraisal company. The RM email /
/// display name and the per-company display names are resolved here via
/// <see cref="IUserLookupService"/> (the Appraisal source carries only ids + amounts).
/// </summary>
public sealed class ShortlistSentToRmEmailHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    IUserLookupService userLookupService,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<ShortlistSentToRmEmailHandler> logger)
    : IConsumer<ShortlistSentToRmEmailIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ShortlistSentToRmEmailIntegrationEvent> context)
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
                    "Skipping quotation fee-notice email: no RM email for RmUsername={RmUsername} (MessageId={MessageId})",
                    msg.RmUsername, context.MessageId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            var adminName = await ResolveNameAsync(msg.AdminUsername, ct);

            // Columns, in order, so each row's cells align positionally.
            var columns = msg.Columns
                .Select(c => new QuotationFeeNoticeColumn(c.ReportNumber ?? "-", c.PropertyType, c.Province))
                .ToList();

            var rows = new List<QuotationFeeNoticeRow>();
            foreach (var row in msg.Rows)
            {
                var companyName = await ResolveCompanyNameAsync(row.CompanyId, ct) ?? row.CompanyId.ToString();
                var cells = msg.Columns
                    .Select(col => row.AmountByAppraisalId.TryGetValue(col.AppraisalId, out var amt)
                        ? amt.ToString("#,##0")
                        : "-")
                    .ToList();
                rows.Add(new QuotationFeeNoticeRow(companyName, cells, row.Total.ToString("#,##0")));
            }

            var subject = $"แจ้งค่าธรรมเนียมประเมิน ลูกค้าราย {msg.CustomerName ?? "-"}";
            var model = new QuotationFeeNoticeModel(rm.Name, msg.CustomerName, columns, rows, adminName);
            var html = templateRenderer.QuotationFeeNotice(subject, model);

            await emailSender.SendAsync(new EmailMessage(
                Subject: subject,
                HtmlBody: html,
                To: [rm.Email],
                Source: "ShortlistSentToRm",
                ReferenceId: msg.QuotationRequestId.ToString()), ct);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error sending quotation fee-notice email (MessageId={MessageId})", context.MessageId);
            throw;
        }
    }

    private async Task<string> ResolveNameAsync(string? username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username)) return string.Empty;
        var info = await userLookupService.GetRequestorAsync(username, ct);
        return info?.Name ?? username;
    }

    private async Task<string?> ResolveCompanyNameAsync(Guid companyId, CancellationToken ct)
    {
        try
        {
            var usernames = await userLookupService.GetUsernamesInRoleAsync("ExtAdmin", companyId, ct);
            if (usernames.Length == 0) return null;
            var lookup = await userLookupService.GetByUsernamesAsync(usernames, ct);
            return lookup.Values.FirstOrDefault(u => u.CompanyName != null)?.CompanyName;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve company name for CompanyId={CompanyId}", companyId);
            return null;
        }
    }
}
