using MassTransit;
using Notification.Notification.Dtos;
using Notification.Notification.Services;

namespace Notification.Notification.EventHandlers;

public class TransitionCompletedNotificationEventHandler : IConsumer<TransitionCompleted>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<TransitionCompletedNotificationEventHandler> _logger;

    public TransitionCompletedNotificationEventHandler(
        INotificationService notificationService,
        ILogger<TransitionCompletedNotificationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransitionCompleted> context)
    {
        var transitionCompleted = context.Message;

        _logger.LogInformation("Processing TransitionCompleted notification for request {RequestId} to state {CurrentState}", 
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
                DateTime.UtcNow
            );

            await _notificationService.SendWorkflowProgressNotificationAsync(notification);
            
            _logger.LogInformation("Successfully sent TransitionCompleted notification for request {RequestId}", 
                transitionCompleted.RequestId);
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
            CompletedAt: index < currentIndex ? DateTime.UtcNow.AddHours(-index) : null
        )).ToList();
    }
}