using MassTransit;
using Notification.Notification.Dtos;
using Notification.Notification.Services;

namespace Notification.Notification.EventHandlers;

public class TaskCompletedNotificationEventHandler : IConsumer<TaskCompleted>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<TaskCompletedNotificationEventHandler> _logger;

    public TaskCompletedNotificationEventHandler(
        INotificationService notificationService,
        ILogger<TaskCompletedNotificationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TaskCompleted> context)
    {
        var taskCompleted = context.Message;

        _logger.LogInformation("Processing TaskCompleted notification for task {TaskName} with action {ActionTaken}", 
            taskCompleted.TaskName, taskCompleted.ActionTaken);

        try
        {
            var notification = new TaskCompletedNotificationDto(
                taskCompleted.CorrelationId,
                taskCompleted.TaskName,
                GetCompletedBy(context), // We need to get this from context or user claims
                taskCompleted.ActionTaken,
                GetRequestIdFromContext(context),
                GetPreviousState(taskCompleted.TaskName),
                GetNextState(taskCompleted.TaskName, taskCompleted.ActionTaken),
                DateTime.UtcNow
            );

            await _notificationService.SendTaskCompletedNotificationAsync(notification);
            
            _logger.LogInformation("Successfully sent TaskCompleted notification for task {TaskName}", 
                taskCompleted.TaskName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TaskCompleted notification for task {TaskName}", 
                taskCompleted.TaskName);
            throw;
        }
    }

    private static long GetRequestIdFromContext(ConsumeContext<TaskCompleted> context)
    {
        if (context.Headers.TryGetHeader("RequestId", out var requestIdObj) && 
            long.TryParse(requestIdObj?.ToString(), out var requestId))
        {
            return requestId;
        }

        return 0;
    }

    private static string GetCompletedBy(ConsumeContext<TaskCompleted> context)
    {
        if (context.Headers.TryGetHeader("CompletedBy", out var completedByObj))
        {
            return completedByObj?.ToString() ?? "System";
        }

        return "System";
    }

    private static string GetPreviousState(string taskName)
    {
        // Map task names to previous states based on workflow
        return taskName switch
        {
            "Admin" => "AwaitingAssignment",
            "AppraisalStaff" => "Admin",
            "AppraisalChecker" => "AppraisalStaff",
            "AppraisalVerifier" => "AppraisalChecker",
            _ => "Unknown"
        };
    }

    private static string GetNextState(string taskName, string actionTaken)
    {
        // Map task names and actions to next states
        if (actionTaken == "R") // Return/Reject
        {
            return taskName switch
            {
                "Admin" => "RequestMaker",
                "AppraisalStaff" => "Admin",
                "AppraisalChecker" => "AppraisalStaff",
                "AppraisalVerifier" => "AppraisalChecker",
                _ => "Unknown"
            };
        }
        else // Proceed
        {
            return taskName switch
            {
                "Admin" => "AppraisalStaff",
                "AppraisalStaff" => "AppraisalChecker",
                "AppraisalChecker" => "AppraisalVerifier",
                "AppraisalVerifier" => "Completed",
                _ => "Unknown"
            };
        }
    }
}