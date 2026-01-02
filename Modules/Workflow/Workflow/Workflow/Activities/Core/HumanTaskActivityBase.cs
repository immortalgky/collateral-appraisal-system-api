using System.Text.Json;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.AssigneeSelection.Engine;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Activities.Core;

/// <summary>
/// Base class for all human task activities that require assignment, bookmarks, and user interaction.
/// Provides common functionality with virtual methods that can be overridden by derived classes.
/// </summary>
public abstract class HumanTaskActivityBase : IWorkflowActivity
{
    protected readonly IWorkflowBookmarkService _bookmarkService;
    protected readonly IWorkflowAuditService _auditService;
    protected readonly ILogger<HumanTaskActivityBase> _logger;

    protected HumanTaskActivityBase(
        IWorkflowBookmarkService bookmarkService,
        IWorkflowAuditService auditService,
        ILogger<HumanTaskActivityBase> logger)
    {
        _bookmarkService = bookmarkService;
        _auditService = auditService;
        _logger = logger;
    }

    public abstract string ActivityType { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }

    /// <summary>
    /// Main execution logic for human task activities
    /// </summary>
    public virtual async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        WorkflowActivityExecution? execution = null;

        try
        {
            // Create and track execution
            execution = CreateActivityExecution(context);
            context.WorkflowInstance.AddActivityExecution(execution);
            context.WorkflowInstance.SetCurrentActivity(context.ActivityId);
            execution.Start();

            // Determine assignee (can be overridden by derived classes)
            var assignmentResult = await DetermineAssigneeAsync(context, cancellationToken);

            if (!assignmentResult.IsSuccess)
            {
                execution.Fail(assignmentResult.ErrorMessage ?? "Assignment failed");
                return ActivityResult.Failed(assignmentResult.ErrorMessage ?? "Assignment failed");
            }

            var assignee = assignmentResult.AssigneeId;
            
            // Set assignee on workflow instance
            context.WorkflowInstance.SetCurrentActivity(context.ActivityId, assignee);

            // Create bookmark for user action
            var bookmarkKey = $"task_{context.WorkflowInstance.Id}_{context.ActivityId}_{Guid.NewGuid():N}";
            var correlationId = context.WorkflowInstance.CorrelationId;
            var bookmarkPayload = CreateBookmarkPayload(context, assignee, assignmentResult);

            try
            {
                await _bookmarkService.CreateUserActionBookmarkAsync(
                    context.WorkflowInstance.Id,
                    context.ActivityId,
                    bookmarkKey,
                    correlationId,
                    bookmarkPayload,
                    cancellationToken);

                _logger.LogInformation(
                    "Created bookmark for task {ActivityId} assigned to {AssigneeId} with key {BookmarkKey}",
                    context.ActivityId, assignee, bookmarkKey);
            }
            catch (Exception bookmarkEx)
            {
                _logger.LogError(bookmarkEx,
                    "Failed to create bookmark for task {ActivityId}. Task is still assigned to {AssigneeId}",
                    context.ActivityId, assignee);
                // Continue even if bookmark creation fails - task is still assigned
            }

            // Log assignment audit trail
            await _auditService.LogAssignmentChangeAsync(
                context,
                null,
                assignee,
                "Initial assignment",
                AssignmentChangeType.InitialAssignment,
                assignmentResult.Metadata,
                cancellationToken);

            // Prepare output data
            var outputData = new Dictionary<string, object>
            {
                ["assigneeRole"] = GetProperty<string>(context, "assigneeRole") ?? "",
                ["assignee"] = assignee,
                ["assignee_group"] = GetProperty<string>(context, "assigneeGroup") ?? "",
                ["assignmentStrategy"] = assignmentResult.Strategy ?? "Default",
                ["assignedTo"] = assignee,
                ["assignmentMetadata"] = assignmentResult.Metadata ?? new Dictionary<string, object>(),
                ["bookmarkKey"] = bookmarkKey,
                [$"{NormalizeActivityId(context.ActivityId)}_assignedTo"] = assignee,
                [$"{NormalizeActivityId(context.ActivityId)}_assignmentMetadata"] = assignmentResult.Metadata ?? new Dictionary<string, object>()
            };

            // Allow derived classes to add custom data
            var customData = await OnExecuteAsync(context, assignee, assignmentResult, cancellationToken);
            foreach (var kvp in customData)
            {
                outputData[kvp.Key] = kvp.Value;
            }

            _logger.LogInformation(
                "Task assigned successfully for activity {ActivityId}. Assignee: {AssigneeId}. Assignment strategy: {Strategy}",
                context.ActivityId, assignee, assignmentResult.Strategy);

            // Human tasks return pending and wait for external completion
            return ActivityResult.Pending(outputData);
        }
        catch (Exception ex)
        {
            execution?.Fail(ex.Message);
            _logger.LogError(ex, "Error executing human task activity {ActivityId}", context.ActivityId);
            return ActivityResult.Failed($"Human task execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Assignment logic that can be overridden by derived classes for simpler assignment.
    /// Default implementation uses direct assignment from properties.
    /// </summary>
    protected virtual Task<AssignmentResult> DetermineAssigneeAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        // Simple assignment logic - can be overridden for complex scenarios
        var assignee = GetProperty<string>(context, "assignee");
        var assigneeRole = GetProperty<string>(context, "assigneeRole");
        var assigneeGroup = GetProperty<string>(context, "assigneeGroup");

        if (!string.IsNullOrEmpty(assignee))
        {
            return Task.FromResult(new AssignmentResult
            {
                IsSuccess = true,
                AssigneeId = assignee,
                Strategy = "DirectAssignment",
                Metadata = new Dictionary<string, object> { ["source"] = "property" }
            });
        }

        if (!string.IsNullOrEmpty(assigneeRole))
        {
            return Task.FromResult(new AssignmentResult
            {
                IsSuccess = true,
                AssigneeId = assigneeRole,
                Strategy = "RoleAssignment",
                Metadata = new Dictionary<string, object> { ["role"] = assigneeRole }
            });
        }

        if (!string.IsNullOrEmpty(assigneeGroup))
        {
            return Task.FromResult(new AssignmentResult
            {
                IsSuccess = true,
                AssigneeId = assigneeGroup,
                Strategy = "GroupAssignment",
                Metadata = new Dictionary<string, object> { ["group"] = assigneeGroup }
            });
        }

        // Default to "Unassigned" if no assignment method is specified
        _logger.LogWarning("No assignee specified for activity {ActivityId}, using default", context.ActivityId);
        return Task.FromResult(new AssignmentResult
        {
            IsSuccess = true,
            AssigneeId = "Unassigned",
            Strategy = "DefaultAssignment",
            Metadata = new Dictionary<string, object>()
        });
    }

    /// <summary>
    /// Hook for derived classes to add custom execute logic
    /// </summary>
    protected virtual Task<Dictionary<string, object>> OnExecuteAsync(
        ActivityContext context,
        string assignee,
        AssignmentResult assignmentResult,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new Dictionary<string, object>());
    }

    /// <summary>
    /// Resume logic for when human task is completed
    /// </summary>
    public virtual async Task<ActivityResult> ResumeAsync(ActivityContext context, Dictionary<string, object> resumeInput, CancellationToken cancellationToken = default)
    {
        var execution = FindActivityExecution(context);
        if (execution == null)
            return ActivityResult.Failed($"No in-progress execution found for activity {context.ActivityId}");

        try
        {
            // Get the completedBy user from resume input
            var completedBy = resumeInput.TryGetValue("completedBy", out var completedByValue)
                ? completedByValue?.ToString()
                : "Unknown";

            // Try to consume the bookmark for this task
            var bookmarkKey = resumeInput.TryGetValue("bookmarkKey", out var keyValue) ? keyValue?.ToString() : null;

            if (!string.IsNullOrEmpty(bookmarkKey))
            {
                try
                {
                    var consumeResult = await _bookmarkService.ConsumeBookmarkAsync(
                        context.WorkflowInstance.Id,
                        context.ActivityId,
                        bookmarkKey,
                        completedBy,
                        null,
                        cancellationToken);
                    if (!consumeResult.Success)
                    {
                        _logger.LogWarning("Failed to consume bookmark {BookmarkKey} for activity {ActivityId}: {ErrorMessage}",
                            bookmarkKey, context.ActivityId, consumeResult.ErrorMessage);
                        // Continue processing even if bookmark consumption fails
                    }
                    else
                    {
                        _logger.LogInformation("Successfully consumed bookmark {BookmarkKey} for activity {ActivityId}",
                            bookmarkKey, context.ActivityId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming bookmark {BookmarkKey} for activity {ActivityId}",
                        bookmarkKey, context.ActivityId);
                }
            }

            // Call derived class for custom resume processing
            var result = await OnResumeAsync(context, resumeInput, cancellationToken);

            // Update execution based on result
            if (result.Status == ActivityResultStatus.Completed)
                execution.Complete(completedBy, result.OutputData, result.Comments);
            else if (result.Status == ActivityResultStatus.Failed)
                execution.Fail(result.ErrorMessage ?? "Activity resume failed");

            return result;
        }
        catch (Exception ex)
        {
            execution.Fail(ex.Message);
            _logger.LogError(ex, "Failed to resume human task activity {ActivityId}", context.ActivityId);
            return ActivityResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Custom resume processing. Can be overridden by derived classes.
    /// </summary>
    protected virtual Task<ActivityResult> OnResumeAsync(ActivityContext context, Dictionary<string, object> resumeInput, CancellationToken cancellationToken)
    {
        var outputData = new Dictionary<string, object>();

        // Pass through common fields
        if (resumeInput.TryGetValue("decision", out var decision))
        {
            outputData["decision"] = decision;
            outputData[$"{NormalizeActivityId(context.ActivityId)}_decision"] = decision;
        }

        if (resumeInput.TryGetValue("comments", out var comments))
        {
            outputData["comments"] = comments;
        }

        outputData["completedBy"] = resumeInput.TryGetValue("completedBy", out var completedBy) ? completedBy : "Unknown";
        outputData["completedAt"] = DateTime.UtcNow;

        return Task.FromResult(ActivityResult.Success(outputData));
    }

    /// <summary>
    /// Validation logic. Can be overridden by derived classes.
    /// </summary>
    public virtual Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Basic validation - at least one assignment method should be specified
        var assignee = GetProperty<string>(context, "assignee");
        var assigneeRole = GetProperty<string>(context, "assigneeRole");
        var assigneeGroup = GetProperty<string>(context, "assigneeGroup");

        if (string.IsNullOrEmpty(assignee) && 
            string.IsNullOrEmpty(assigneeRole) && 
            string.IsNullOrEmpty(assigneeGroup))
        {
            errors.Add("At least one assignment method must be specified (assignee, assigneeRole, or assigneeGroup)");
        }

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    // Helper methods
    protected T GetProperty<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        if (context.Properties.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return defaultValue;
    }

    protected T GetVariable<T>(ActivityContext context, string key, T defaultValue = default!)
    {
        if (context.Variables.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return defaultValue;
    }

    protected virtual WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            context.CurrentAssignee ?? "Unassigned",
            context.Variables);
    }

    protected virtual string CreateBookmarkPayload(ActivityContext context, string assignee, AssignmentResult assignmentResult)
    {
        return JsonSerializer.Serialize(new
        {
            activityId = context.ActivityId,
            activityType = ActivityType,
            assignee,
            assignmentStrategy = assignmentResult.Strategy,
            createdAt = DateTime.UtcNow,
            metadata = assignmentResult.Metadata
        });
    }

    protected WorkflowActivityExecution? FindActivityExecution(ActivityContext context)
    {
        return context.WorkflowInstance.ActivityExecutions
            .FirstOrDefault(e => e.ActivityId == context.ActivityId && e.Status == ActivityExecutionStatus.InProgress);
    }

    protected string NormalizeActivityId(string activityId)
    {
        return activityId.Replace("-", "_").Replace(" ", "_");
    }
}

/// <summary>
/// Result of assignment determination process
/// </summary>
public class AssignmentResult
{
    public bool IsSuccess { get; set; }
    public string? AssigneeId { get; set; }
    public string? Strategy { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? ErrorMessage { get; set; }
}