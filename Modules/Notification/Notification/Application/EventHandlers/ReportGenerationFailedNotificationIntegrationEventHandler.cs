using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Consumes <see cref="ReportGenerationFailedIntegrationEvent"/> from the Reporting module and
/// persists a durable <c>UserNotification</c> (plus realtime push) so the requesting user learns a
/// background report failed even if their SignalR connection was down at the time.
/// </summary>
public class ReportGenerationFailedNotificationIntegrationEventHandler
    : IConsumer<ReportGenerationFailedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ReportGenerationFailedNotificationIntegrationEventHandler> _logger;
    private readonly InboxGuard<NotificationDbContext> _inboxGuard;

    public ReportGenerationFailedNotificationIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<ReportGenerationFailedNotificationIntegrationEventHandler> logger,
        InboxGuard<NotificationDbContext> inboxGuard)
    {
        _notificationService = notificationService;
        _logger = logger;
        _inboxGuard = inboxGuard;
    }

    public async Task Consume(ConsumeContext<ReportGenerationFailedIntegrationEvent> context)
    {
        if (await _inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;

        try
        {
            await _notificationService.SendNotificationToUserAsync(
                msg.RequestedByCode,
                "Report failed",
                $"Your report '{msg.ReportTypeKey}' could not be generated. Please try again.",
                NotificationType.ReportFailed,
                metadata: new Dictionary<string, object>
                {
                    ["jobId"] = msg.JobId,
                    ["reportTypeKey"] = msg.ReportTypeKey,
                    ["error"] = msg.Error,
                });

            await _inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ReportGenerationFailed notification for job {JobId}", msg.JobId);
            throw;
        }
    }
}
