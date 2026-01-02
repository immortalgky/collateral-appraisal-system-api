using Microsoft.Extensions.Logging;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Activities.Timers;

/// <summary>
/// Cron activity that schedules execution based on cron expressions
/// </summary>
public class CronActivity : WorkflowActivityBase
{
    private readonly IWorkflowBookmarkService _bookmarkService;
    private readonly ILogger<CronActivity> _logger;

    public override string ActivityType => "CronActivity";
    public override string Name => "Cron Schedule";
    public override string Description => "Schedules execution based on cron expressions";

    // Cron configuration properties
    public string CronExpression { get; set; } = "0 0 * * *"; // Daily at midnight
    public string? TimeZone { get; set; } = "UTC";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxOccurrences { get; set; }
    public string? CronName { get; set; }
    public bool AllowManualTrigger { get; set; } = true;

    public CronActivity(
        IWorkflowBookmarkService bookmarkService,
        ILogger<CronActivity> logger)
    {
        _bookmarkService = bookmarkService;
        _logger = logger;
    }

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing cron activity {ActivityId} for instance {InstanceId} with expression: {CronExpression}",
            context.ActivityId, context.WorkflowInstanceId, CronExpression);

        try
        {
            // Validate cron expression
            if (!IsValidCronExpression(CronExpression))
            {
                return ActivityResult.Failed($"Invalid cron expression: {CronExpression}");
            }

            // Calculate next execution time
            var nextExecution = CalculateNextExecution(CronExpression, DateTime.UtcNow);
            if (nextExecution == null)
            {
                _logger.LogInformation("No future execution scheduled for cron {CronExpression}", CronExpression);
                return ActivityResult.Success(new Dictionary<string, object>
                {
                    ["CompletedAt"] = DateTime.UtcNow,
                    ["Reason"] = "No future execution scheduled"
                });
            }

            // Check if within valid date range
            if (IsOutsideDateRange(nextExecution.Value))
            {
                _logger.LogInformation("Next execution {NextExecution} is outside allowed date range", nextExecution);
                return ActivityResult.Success(new Dictionary<string, object>
                {
                    ["CompletedAt"] = DateTime.UtcNow,
                    ["Reason"] = "Outside date range"
                });
            }

            // Create bookmark for cron schedule
            var bookmarkName = CreateCronBookmark(context, nextExecution.Value);
            
            // Schedule the cron job
            await ScheduleCronJobAsync(context.WorkflowInstanceId, context.ActivityId, bookmarkName, nextExecution.Value, cancellationToken);

            _logger.LogInformation("Cron scheduled for next execution at: {NextExecution} (bookmark: {BookmarkName})", 
                nextExecution, bookmarkName);

            return ActivityResult.Pending(new Dictionary<string, object>
            {
                ["NextExecution"] = nextExecution.Value,
                ["CronExpression"] = CronExpression,
                ["CronName"] = CronName ?? "CronJob",
                ["TimeZone"] = TimeZone ?? "UTC",
                ["AllowManualTrigger"] = AllowManualTrigger
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cron activity execution failed for activity {ActivityId}", context.ActivityId);
            return ActivityResult.Failed($"Cron execution failed: {ex.Message}");
        }
    }


    /// <summary>
    /// Manually trigger the cron job before its scheduled time
    /// </summary>
    public async Task<bool> TriggerManuallyAsync(Guid workflowInstanceId, string activityId, CancellationToken cancellationToken = default)
    {
        if (!AllowManualTrigger)
        {
            _logger.LogWarning("Manual trigger not allowed for cron activity {ActivityId}", activityId);
            return false;
        }

        try
        {
            var bookmarkName = $"Cron_{workflowInstanceId}_{activityId}";
            
            // Consume the bookmark with manual trigger data
            var result = await _bookmarkService.ConsumeBookmarkAsync(
                workflowInstanceId,
                activityId,
                bookmarkName,
                "User",
                new Dictionary<string, object> 
                { 
                    ["ManualTrigger"] = true,
                    ["ExecutionTime"] = DateTime.UtcNow
                },
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Cron manually triggered successfully for activity {ActivityId}", activityId);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to manually trigger cron for activity {ActivityId}: {Error}", activityId, result.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error manually triggering cron for activity {ActivityId}", activityId);
            return false;
        }
    }


    #region Private Helper Methods

    private string CreateCronBookmark(ActivityContext context, DateTime nextExecution)
    {
        var bookmarkName = $"Cron_{context.WorkflowInstanceId}_{context.ActivityId}";
        return bookmarkName;
    }

    private async Task ScheduleCronJobAsync(Guid workflowInstanceId, string activityId, string bookmarkName, DateTime nextExecution, CancellationToken cancellationToken)
    {
        // In a production system, this would integrate with a cron/scheduler service
        _logger.LogInformation("Scheduling cron job for workflow {InstanceId}, activity {ActivityId} at {NextExecution}",
            workflowInstanceId, activityId, nextExecution);
        
        // In a real implementation, you would:
        // 1. Store the cron schedule in a persistent scheduler
        // 2. Use a background service like Hangfire, Quartz.NET, or similar
        // 3. When cron triggers, consume the bookmark to resume workflow
        
        // Example pseudo-code for production:
        // await _cronService.ScheduleJobAsync(workflowInstanceId, activityId, bookmarkName, CronExpression, nextExecution);
    }

    private static bool IsValidCronExpression(string cronExpression)
    {
        // Simplified validation - in production, use a proper cron expression parser
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;

        var parts = cronExpression.Split(' ');
        return parts.Length == 5 || parts.Length == 6; // Standard cron (5) or with seconds (6)
    }

    private DateTime? CalculateNextExecution(string cronExpression, DateTime fromTime)
    {
        // Simplified cron calculation - in production, use a proper cron library like Cronos
        try
        {
            // For demo purposes, return a simple next execution time
            // In reality, you would parse the cron expression and calculate the next valid time

            if (cronExpression == "0 0 * * *") // Daily at midnight
            {
                var nextMidnight = fromTime.Date.AddDays(1);
                return nextMidnight;
            }
            else if (cronExpression == "0 * * * *") // Every hour
            {
                var nextHour = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, fromTime.Hour, 0, 0).AddHours(1);
                return nextHour;
            }
            else if (cronExpression == "* * * * *") // Every minute
            {
                var nextMinute = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, fromTime.Hour, fromTime.Minute, 0).AddMinutes(1);
                return nextMinute;
            }
            else
            {
                // Default to next hour for unknown expressions
                return fromTime.AddHours(1);
            }
        }
        catch
        {
            return null;
        }
    }

    private bool IsOutsideDateRange(DateTime dateTime)
    {
        if (StartDate.HasValue && dateTime < StartDate.Value)
            return true;

        if (EndDate.HasValue && dateTime > EndDate.Value)
            return true;

        return false;
    }

    #endregion
}