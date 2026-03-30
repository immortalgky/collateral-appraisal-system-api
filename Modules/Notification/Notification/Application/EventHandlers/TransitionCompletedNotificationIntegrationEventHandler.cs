using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Dtos;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

public class TransitionCompletedNotificationIntegrationEventHandler : IConsumer<TransitionCompletedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<TransitionCompletedNotificationIntegrationEventHandler> _logger;
    private readonly InboxGuard<NotificationDbContext> _inboxGuard;

    public TransitionCompletedNotificationIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<TransitionCompletedNotificationIntegrationEventHandler> logger,
        InboxGuard<NotificationDbContext> inboxGuard)
    {
        _notificationService = notificationService;
        _logger = logger;
        _inboxGuard = inboxGuard;
    }

    public async Task Consume(ConsumeContext<TransitionCompletedIntegrationEvent> context)
    {
        if (await _inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var transitionCompleted = context.Message;

        _logger.LogInformation(
            "Processing TransitionCompleted notification for request {RequestId} to state {CurrentState}",
            transitionCompleted.RequestId, transitionCompleted.CurrentState);

        try
        {
            var workflowSteps = BuildWorkflowSteps(transitionCompleted.CurrentState);

            var notification = new WorkflowProgressNotificationDto(
                transitionCompleted.CorrelationId,
                transitionCompleted.RequestId,
                transitionCompleted.CurrentState,
                transitionCompleted.AssignedTo,
                transitionCompleted.AssignedType,
                workflowSteps,
                DateTime.Now
            );

            await _notificationService.SendWorkflowProgressNotificationAsync(notification);

            _logger.LogInformation("Successfully sent TransitionCompleted notification for request {RequestId}",
                transitionCompleted.RequestId);

            await _inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TransitionCompleted notification for request {RequestId}",
                transitionCompleted.RequestId);
            throw;
        }
    }

    private static List<WorkflowStepDto> BuildWorkflowSteps(string currentState)
    {
        var allStates = new[]
        {
            "AwaitingAssignment",
            "Admin",
            "AppraisalStaff",
            "AppraisalChecker",
            "AppraisalVerifier"
        };

        var currentIndex = Array.IndexOf(allStates, currentState);

        return allStates.Select((state, index) => new WorkflowStepDto(
            state,
            IsCompleted: index < currentIndex,
            IsCurrent: index == currentIndex,
            AssignedTo: index == currentIndex ? "Current User" : null,
            CompletedAt: index < currentIndex ? DateTime.Now.AddHours(-index) : null
        )).ToList();
    }
}
