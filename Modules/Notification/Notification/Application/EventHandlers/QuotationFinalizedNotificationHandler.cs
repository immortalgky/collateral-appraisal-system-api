using Auth.Contracts.Users;
using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies the RM that a quotation has been finalized, including the winning company name and fee.
/// The winning company's group send has been replaced with per-user delivery via QuotationStarted flow.
/// </summary>
public class QuotationFinalizedNotificationHandler(
    INotificationService notificationService,
    IUserLookupService userLookupService,
    ILogger<QuotationFinalizedNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<QuotationFinalizedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationFinalizedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Processing QuotationFinalized notification for QuotationRequestId={QuotationRequestId}, WinningCompanyId={WinningCompanyId}",
            message.QuotationRequestId, message.WinningCompanyId);

        try
        {
            if (string.IsNullOrEmpty(message.RmUsername))
            {
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            // Resolve winning company name via an ExtAdmin user of that company
            var companyName = await ResolveCompanyNameAsync(message.WinningCompanyId, ct);
            var winnerDescription = companyName ?? message.WinningCompanyId.ToString();

            var metadata = new Dictionary<string, object>
            {
                { "quotationRequestId", message.QuotationRequestId },
                { "appraisalId", message.AppraisalId },
                { "winningCompanyId", message.WinningCompanyId },
                { "finalFeeAmount", message.FinalFeeAmount }
            };

            await notificationService.SendNotificationToUserAsync(
                message.RmUsername,
                "Quotation Finalized",
                $"Quotation finalized — winner: {winnerDescription}, fee {message.FinalFeeAmount:N2} THB.",
                NotificationType.WorkflowTransition,
                metadata: metadata);

            logger.LogInformation(
                "Sent QuotationFinalized notification to RM {RmUsername} for QuotationRequestId={QuotationRequestId}",
                message.RmUsername, message.QuotationRequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing QuotationFinalized notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);
            throw;
        }
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
            logger.LogWarning(ex,
                "Could not resolve company name for CompanyId={CompanyId} — using ID fallback",
                companyId);
            return null;
        }
    }
}
