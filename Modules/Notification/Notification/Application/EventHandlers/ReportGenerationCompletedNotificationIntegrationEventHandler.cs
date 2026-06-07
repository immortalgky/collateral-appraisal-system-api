using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Consumes <see cref="ReportGenerationCompletedIntegrationEvent"/> from the Reporting module and
/// persists a durable <c>UserNotification</c> (bell entry, recoverable on reconnect/login) plus a
/// realtime <c>ReceiveNotification</c> push — closing the gap where a dropped SignalR connection at
/// completion would otherwise silently lose the "report ready" notice.
/// </summary>
public class ReportGenerationCompletedNotificationIntegrationEventHandler
    : IConsumer<ReportGenerationCompletedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ReportGenerationCompletedNotificationIntegrationEventHandler> _logger;
    private readonly InboxGuard<NotificationDbContext> _inboxGuard;

    public ReportGenerationCompletedNotificationIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<ReportGenerationCompletedNotificationIntegrationEventHandler> logger,
        InboxGuard<NotificationDbContext> inboxGuard)
    {
        _notificationService = notificationService;
        _logger = logger;
        _inboxGuard = inboxGuard;
    }

    public async Task Consume(ConsumeContext<ReportGenerationCompletedIntegrationEvent> context)
    {
        if (await _inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;

        try
        {
            await _notificationService.SendNotificationToUserAsync(
                msg.RequestedByCode,
                "Report ready",
                $"Your report '{msg.ReportTypeKey}' is ready to download.",
                NotificationType.ReportReady,
                actionUrl: $"/reports/jobs/{msg.JobId}/download",
                metadata: new Dictionary<string, object>
                {
                    ["jobId"] = msg.JobId,
                    ["reportTypeKey"] = msg.ReportTypeKey,
                    ["fileName"] = msg.FileName,
                });

            await _inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ReportGenerationCompleted notification for job {JobId}", msg.JobId);
            throw;
        }
    }
}
