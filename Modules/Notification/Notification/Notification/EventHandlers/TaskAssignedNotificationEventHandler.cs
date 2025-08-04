using MassTransit;
using Notification.Notification.Dtos;
using Notification.Notification.Services;

namespace Notification.Notification.EventHandlers;

public class TaskAssignedNotificationEventHandler : IConsumer<TaskAssigned>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<TaskAssignedNotificationEventHandler> _logger;

    public TaskAssignedNotificationEventHandler(
        INotificationService notificationService,
        ILogger<TaskAssignedNotificationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TaskAssigned> context)
    {
        var taskAssigned = context.Message;

        _logger.LogInformation("Processing TaskAssigned notification for user {AssignedTo} and task {TaskName}", 
            taskAssigned.AssignedTo, taskAssigned.TaskName);

        try
        {
            var notification = new TaskAssignedNotificationDto(
                taskAssigned.CorrelationId,
                taskAssigned.TaskName,
                taskAssigned.AssignedTo,
                taskAssigned.AssignedType,
                // We need to get request ID from saga state or context
                GetRequestIdFromContext(context),
                taskAssigned.TaskName, // Using TaskName as current state for now
                DateTime.UtcNow
            );

            await _notificationService.SendTaskAssignedNotificationAsync(notification);
            
            _logger.LogInformation("Successfully sent TaskAssigned notification for user {AssignedTo}", 
                taskAssigned.AssignedTo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TaskAssigned notification for user {AssignedTo} and task {TaskName}", 
                taskAssigned.AssignedTo, taskAssigned.TaskName);
            throw;
        }
    }

    private static long GetRequestIdFromContext(ConsumeContext<TaskAssigned> context)
    {
        // Try to get request ID from message headers or correlation ID
        if (context.Headers.TryGetHeader("RequestId", out var requestIdObj) && 
            long.TryParse(requestIdObj?.ToString(), out var requestId))
        {
            return requestId;
        }

        // Fallback: could extract from correlation ID or use a default
        return 0; // This should be improved with proper request ID propagation
    }
}