using Microsoft.Extensions.Logging;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Activities.Timers;

/// <summary>
/// Timer activity that waits for a specified duration before continuing
/// </summary>
public class TimerActivity : WorkflowActivityBase
{
    private readonly IWorkflowBookmarkService _bookmarkService;
    private readonly ILogger<TimerActivity> _logger;

    public override string ActivityType => "TimerActivity";
    public override string Name => "Timer Wait";
    public override string Description => "Waits for a specified duration before continuing execution";

    // Timer configuration properties
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);
    public DateTime? ScheduledTime { get; set; }
    public string? TimerName { get; set; }
    public bool AllowEarlyCancellation { get; set; } = true;

    public TimerActivity(
        IWorkflowBookmarkService bookmarkService,
        ILogger<TimerActivity> logger)
    {
        _bookmarkService = bookmarkService;
        _logger = logger;
    }

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing timer activity {ActivityId} for instance {InstanceId}",
            context.ActivityId, context.WorkflowInstanceId);

        try
        {
            // Determine the target time
            DateTime targetTime;
            if (ScheduledTime.HasValue)
            {
                targetTime = ScheduledTime.Value;
                _logger.LogInformation("Timer scheduled for specific time: {ScheduledTime}", targetTime);
            }
            else
            {
                targetTime = DateTime.UtcNow.Add(Duration);
                _logger.LogInformation("Timer scheduled for duration: {Duration} (target: {TargetTime})", Duration, targetTime);
            }

            // Check if the target time has already passed
            if (targetTime <= DateTime.UtcNow)
            {
                _logger.LogInformation("Timer target time already passed, completing immediately");
                return ActivityResult.Success(new Dictionary<string, object>
                {
                    ["CompletedAt"] = DateTime.UtcNow,
                    ["WasDelayed"] = false
                });
            }

            // Create a bookmark to pause execution until the timer expires
            var bookmarkName = CreateTimerBookmark(context, targetTime);
            
            // Schedule the timer using a background service (in production, use a proper timer service)
            await ScheduleTimerAsync(context.WorkflowInstanceId, context.ActivityId, bookmarkName, targetTime, cancellationToken);

            _logger.LogInformation("Timer bookmark created: {BookmarkName} for target time: {TargetTime}", 
                bookmarkName, targetTime);

            // Return pending status (workflow will be suspended via bookmark)
            return ActivityResult.Pending(new Dictionary<string, object>
            {
                ["ScheduledTime"] = targetTime,
                ["TimerName"] = TimerName ?? "Timer",
                ["Duration"] = Duration.ToString(),
                ["AllowEarlyCancellation"] = AllowEarlyCancellation
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Timer activity execution failed for activity {ActivityId}", context.ActivityId);
            return ActivityResult.Failed($"Timer execution failed: {ex.Message}");
        }
    }


    /// <summary>
    /// Cancel the timer before it expires
    /// </summary>
    public async Task<bool> CancelTimerAsync(Guid workflowInstanceId, string activityId, CancellationToken cancellationToken = default)
    {
        if (!AllowEarlyCancellation)
        {
            _logger.LogWarning("Timer cancellation not allowed for activity {ActivityId}", activityId);
            return false;
        }

        try
        {
            var bookmarkName = $"Timer_{workflowInstanceId}_{activityId}";
            
            // Consume the bookmark with cancellation data
            var result = await _bookmarkService.ConsumeBookmarkAsync(
                workflowInstanceId, 
                activityId, 
                bookmarkName, 
                "System", 
                new Dictionary<string, object> { ["Cancelled"] = true },
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Timer cancelled successfully for activity {ActivityId}", activityId);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to cancel timer for activity {ActivityId}: {Error}", activityId, result.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling timer for activity {ActivityId}", activityId);
            return false;
        }
    }


    #region Private Helper Methods

    private string CreateTimerBookmark(ActivityContext context, DateTime targetTime)
    {
        var bookmarkName = $"Timer_{context.WorkflowInstanceId}_{context.ActivityId}";
        
        // In a real implementation, you would create the bookmark through the bookmark service
        // For now, we return the bookmark name
        return bookmarkName;
    }

    private async Task ScheduleTimerAsync(Guid workflowInstanceId, string activityId, string bookmarkName, DateTime targetTime, CancellationToken cancellationToken)
    {
        // In a production system, this would integrate with a timer/scheduler service
        // For now, we simulate scheduling by logging
        _logger.LogInformation("Scheduling timer for workflow {InstanceId}, activity {ActivityId} at {TargetTime}",
            workflowInstanceId, activityId, targetTime);
        
        // In a real implementation, you would:
        // 1. Store the timer in a persistent timer queue/database
        // 2. Use a background service to monitor and trigger timers
        // 3. When timer expires, consume the bookmark to resume workflow
        
        // Example pseudo-code for production:
        // await _timerService.ScheduleTimerAsync(workflowInstanceId, activityId, bookmarkName, targetTime);
    }

    #endregion
}