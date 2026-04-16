using MassTransit;
using Shared.Messaging.Events;

namespace Notification.Domain.Notifications.EventHandlers;

public class SlaBreachNotificationIntegrationEventHandler : IConsumer<SlaBreachIntegrationEvent>
{
    private readonly ILogger<SlaBreachNotificationIntegrationEventHandler> _logger;

    public SlaBreachNotificationIntegrationEventHandler(ILogger<SlaBreachNotificationIntegrationEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SlaBreachIntegrationEvent> context)
    {
        var breach = context.Message;

        _logger.LogInformation(
            "Processing SLA breach notification: Task {TaskName} assigned to {AssignedTo}, Status={SlaStatus}, DueAt={DueAt}",
            breach.TaskName, breach.AssignedTo, breach.SlaStatus, breach.DueAt);

        try
        {
            // TODO: Wire up to INotificationService to send actual notifications
            // TODO: Add InboxGuard<NotificationDbContext> idempotency guard to prevent duplicate
            //       notifications on message retry (same pattern as TaskAssignedNotificationIntegrationEventHandler)
            // For now, log the breach event for monitoring
            if (breach.SlaStatus == "Breached")
            {
                _logger.LogWarning(
                    "SLA BREACH NOTIFICATION: Task {TaskName} (CorrelationId={CorrelationId}) assigned to {AssignedTo} has exceeded its deadline of {DueAt}",
                    breach.TaskName, breach.CorrelationId, breach.AssignedTo, breach.DueAt);
            }
            else if (breach.SlaStatus == "AtRisk")
            {
                _logger.LogWarning(
                    "SLA AT-RISK NOTIFICATION: Task {TaskName} (CorrelationId={CorrelationId}) assigned to {AssignedTo} is approaching its deadline of {DueAt}",
                    breach.TaskName, breach.CorrelationId, breach.AssignedTo, breach.DueAt);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing SLA breach notification for task {TaskName}, assigned to {AssignedTo}",
                breach.TaskName, breach.AssignedTo);
            throw;
        }
    }
}
