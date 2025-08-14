using Assignment.Workflow.Activities;
using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Activities.Factories;
using Assignment.Workflow.Models;
using Assignment.Workflow.Repositories;
using Assignment.Workflow.Schema;
using Shared.Extensions;

namespace Assignment.Workflow.Engine;

public class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowActivityFactory _activityFactory;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowActivityFactory activityFactory,
        IPublishEndpoint publishEndpoint,
        ILogger<WorkflowEngine> logger)
    {
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _activityFactory = activityFactory;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var workflowDefinition =
            await _workflowDefinitionRepository.GetByIdAsync(workflowDefinitionId, cancellationToken);
        if (workflowDefinition == null)
        {
            throw new InvalidOperationException($"Workflow definition not found: {workflowDefinitionId}");
        }

        var workflowSchema = JsonSerializer.Deserialize<WorkflowSchema>(workflowDefinition.JsonDefinition);
        if (workflowSchema == null)
        {
            throw new InvalidOperationException($"Invalid workflow definition JSON for: {workflowDefinitionId}");
        }

        // Find start activity
        var startActivity = workflowSchema.Activities.FirstOrDefault(a => a.IsStartActivity)
                            ?? workflowSchema.Activities.First();

        // Create workflow instance
        var workflowInstance = WorkflowInstance.Create(
            workflowDefinitionId,
            instanceName,
            correlationId,
            startedBy,
            initialVariables);

        workflowInstance.SetCurrentActivity(startActivity.Id);

        _logger.LogInformation("Started workflow instance {WorkflowInstanceId} for definition {WorkflowDefinitionId}",
            workflowInstance.Id, workflowDefinitionId);

        // Execute first activity
        await ExecuteActivityAsync(workflowInstance, workflowSchema, startActivity, cancellationToken);

        await _workflowInstanceRepository.AddAsync(workflowInstance, cancellationToken);
        await _workflowInstanceRepository.SaveChangesAsync(cancellationToken);

        // Publish workflow started event
        await _publishEndpoint.Publish(new WorkflowStarted
        {
            WorkflowInstanceId = workflowInstance.Id,
            WorkflowDefinitionId = workflowDefinitionId,
            InstanceName = instanceName,
            StartedBy = startedBy,
            StartedAt = workflowInstance.StartedOn,
            CorrelationId = correlationId
        }, cancellationToken);

        return workflowInstance;
    }

    public async Task<WorkflowInstance> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        Dictionary<string, object> outputData,
        string completedBy,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var workflowInstance =
            await _workflowInstanceRepository.GetByIdAsync(workflowInstanceId, cancellationToken);
        if (workflowInstance == null)
        {
            throw new InvalidOperationException($"Workflow instance not found: {workflowInstanceId}");
        }

        var workflowDefinition =
            await _workflowDefinitionRepository.GetByIdAsync(workflowInstance.WorkflowDefinitionId, cancellationToken);
        var workflowSchema = JsonSerializer.Deserialize<WorkflowSchema>(workflowDefinition!.JsonDefinition);

        // Find current activity execution
        var activityExecution = workflowInstance.ActivityExecutions
            .FirstOrDefault(ae => ae.ActivityId == activityId && ae.Status == ActivityExecutionStatus.InProgress);

        if (activityExecution == null)
        {
            throw new InvalidOperationException($"Activity execution not found or not in progress: {activityId}");
        }

        // Complete the activity execution
        activityExecution.Complete(completedBy, outputData, comments);

        // Convert JsonElement values to proper types
        var convertedOutputData = outputData.ConvertJsonElements();

        // Update workflow variables
        Console.WriteLine(convertedOutputData.GetType().Name);
        if (convertedOutputData.ContainsKey("variableUpdates") &&
            convertedOutputData["variableUpdates"] is Dictionary<string, object> variableUpdates)
        {
            workflowInstance.UpdateVariables(variableUpdates);
        }

        // Determine next activity using transitions
        var activityResult = new ActivityResult
        {
            Status = ActivityResultStatus.Completed,
            OutputData = convertedOutputData
        };

        var nextActivityId =
            await DetermineNextActivityAsync(workflowSchema!, activityId, activityResult, workflowInstance);
        if (!string.IsNullOrEmpty(nextActivityId))
        {
            var nextActivity = workflowSchema!.Activities.FirstOrDefault(a => a.Id == nextActivityId);
            if (nextActivity != null)
            {
                workflowInstance.SetCurrentActivity(nextActivity.Id);
                await ExecuteActivityAsync(workflowInstance, workflowSchema, nextActivity, cancellationToken);
            }
        }
        else
        {
            // No next activity, workflow is completed
            workflowInstance.UpdateStatus(WorkflowStatus.Completed);
            _logger.LogInformation("Workflow instance {WorkflowInstanceId} completed", workflowInstanceId);
        }

        await _workflowInstanceRepository.SaveChangesAsync(cancellationToken);

        // Publish activity completed event
        await _publishEndpoint.Publish(new WorkflowActivityCompleted
        {
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            CompletedBy = completedBy,
            CompletedAt = DateTime.Now,
            OutputData = outputData,
            Comments = comments
        }, cancellationToken);

        return workflowInstance;
    }

    public async Task<WorkflowInstance?> GetWorkflowInstanceAsync(Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await _workflowInstanceRepository.GetWithExecutionsAsync(workflowInstanceId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetUserTasksAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        return await _workflowInstanceRepository.GetByAssignee(userId, cancellationToken);
    }

    public async Task<bool> ValidateWorkflowDefinitionAsync(WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic validation
            if (string.IsNullOrEmpty(workflowSchema.Name)) return false;
            if (!workflowSchema.Activities.Any()) return false;

            // Validate each activity
            foreach (var activity in workflowSchema.Activities)
            {
                var workflowActivity = _activityFactory.CreateActivity(activity.Type);
                var context = new ActivityContext
                {
                    ActivityId = activity.Id,
                    Properties = activity.Properties,
                    Variables = workflowSchema.Variables
                };

                var validationResult = await workflowActivity.ValidateAsync(context, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Activity {ActivityId} validation failed: {Errors}",
                        activity.Id, string.Join(", ", validationResult.Errors));
                    return false;
                }
            }

            // Validate transitions
            foreach (var transition in workflowSchema.Transitions)
            {
                var fromActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == transition.From);
                var toActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == transition.To);

                if (fromActivity == null || toActivity == null)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow definition");
            return false;
        }
    }

    public async Task CancelWorkflowAsync(Guid workflowInstanceId, string cancelledBy, string reason,
        CancellationToken cancellationToken = default)
    {
        var workflowInstance =
            await _workflowInstanceRepository.GetByIdAsync(workflowInstanceId, cancellationToken);
        if (workflowInstance == null)
        {
            throw new InvalidOperationException($"Workflow instance not found: {workflowInstanceId}");
        }

        workflowInstance.UpdateStatus(WorkflowStatus.Cancelled, reason);
        await _workflowInstanceRepository.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new WorkflowCancelled
        {
            WorkflowInstanceId = workflowInstanceId,
            CancelledBy = cancelledBy,
            CancelledAt = DateTime.Now,
            Reason = reason
        }, cancellationToken);

        _logger.LogInformation("Workflow instance {WorkflowInstanceId} cancelled by {CancelledBy}: {Reason}",
            workflowInstanceId, cancelledBy, reason);
    }

    private Task<string?> DetermineNextActivityAsync(
        WorkflowSchema workflowSchema,
        string currentActivityId,
        ActivityResult activityResult,
        WorkflowInstance workflowInstance)
    {
        // First check if activity explicitly specified next activity (fallback for legacy behavior)
        if (!string.IsNullOrEmpty(activityResult.NextActivityId))
        {
            return Task.FromResult<string?>(activityResult.NextActivityId);
        }

        // Find transitions from current activity
        var transitions = workflowSchema.Transitions
            .Where(t => t.From == currentActivityId)
            .OrderBy(t => t.Type == TransitionType.Normal ? 1 : 0) // Prioritize conditional transitions
            .ToList();

        if (!transitions.Any())
        {
            return Task.FromResult<string?>(null); // No transitions defined, workflow ends
        }

        // For decision activities, check output data for decision result
        var currentActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == currentActivityId);
        if (currentActivity?.Type == ActivityTypes.DecisionActivity)
        {
            // Look for decision result in output data
            if (activityResult.OutputData.TryGetValue("decision", out var decisionValue))
            {
                var decision = decisionValue?.ToString();

                // Find transition matching the decision
                var decisionTransition = transitions.FirstOrDefault(t =>
                    t.Type == TransitionType.Conditional &&
                    EvaluateTransitionCondition(t, decision, workflowInstance));

                if (decisionTransition != null)
                {
                    return Task.FromResult<string?>(decisionTransition.To);
                }
            }
        }

        // Evaluate conditional transitions
        foreach (var transition in transitions.Where(t => t.Type == TransitionType.Conditional))
        {
            if (EvaluateTransitionCondition(transition, null, workflowInstance))
            {
                return Task.FromResult<string?>(transition.To);
            }
        }

        // Return first normal transition as default
        var normalTransition = transitions.FirstOrDefault(t => t.Type == TransitionType.Normal);
        return Task.FromResult(normalTransition?.To);
    }

    private bool EvaluateTransitionCondition(TransitionDefinition transition, string? decisionValue,
        WorkflowInstance workflowInstance)
    {
        if (string.IsNullOrEmpty(transition.Condition))
            return true; // No condition means always true

        // For decision-based transitions, check if condition matches decision
        if (!string.IsNullOrEmpty(decisionValue))
        {
            return string.Equals(transition.Condition, decisionValue, StringComparison.OrdinalIgnoreCase);
        }

        // For other conditional transitions, evaluate against workflow variables
        try
        {
            var condition = transition.Condition;
            var parts = condition.Split(new[] { "==", "!=", ">=", "<=", ">", "<" },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            var variable = parts[0].Trim();
            var expectedValue = parts[1].Trim().Trim('\'', '"');

            if (!workflowInstance.Variables.TryGetValue(variable, out var actualValueObj))
                return false;

            var actualValue = actualValueObj?.ToString() ?? string.Empty;

            var op = condition.Contains("==") ? "==" :
                condition.Contains("!=") ? "!=" :
                condition.Contains(">=") ? ">=" :
                condition.Contains("<=") ? "<=" :
                condition.Contains(">") ? ">" :
                condition.Contains("<") ? "<" : "==";

            return op switch
            {
                "==" => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
                _ => true // For now, default to true for unsupported operations
            };
        }
        catch
        {
            return false;
        }
    }

    private async Task ExecuteActivityAsync(
        WorkflowInstance workflowInstance,
        WorkflowSchema workflowSchema,
        ActivityDefinition activityDefinition,
        CancellationToken cancellationToken)
    {
        var activitiesToExecute = new Queue<ActivityDefinition>();
        activitiesToExecute.Enqueue(activityDefinition);

        while (activitiesToExecute.Count > 0)
        {
            var currentActivity = activitiesToExecute.Dequeue();

            try
            {
                // Create an activity execution record
                var activityExecution = WorkflowActivityExecution.Create(
                    workflowInstance.Id,
                    currentActivity.Id,
                    currentActivity.Name,
                    currentActivity.Type,
                    workflowInstance.CurrentAssignee,
                    workflowInstance.Variables);

                workflowInstance.AddActivityExecution(activityExecution);
                activityExecution.Start();

                // Create and execute activity
                var activity = _activityFactory.CreateActivity(currentActivity.Type);
                var context = new ActivityContext
                {
                    WorkflowInstanceId = workflowInstance.Id,
                    ActivityId = currentActivity.Id,
                    Properties = currentActivity.Properties,
                    Variables = workflowInstance.Variables,
                    InputData = new Dictionary<string, object>(),
                    CurrentAssignee = workflowInstance.CurrentAssignee
                };

                var result = await activity.ExecuteAsync(context, cancellationToken);

                if (result.Status == ActivityResultStatus.Completed)
                {
                    activityExecution.Complete("system", result.OutputData, result.Comments);

                    // Update workflow variables
                    if (result.VariableUpdates.Any())
                    {
                        workflowInstance.UpdateVariables(result.VariableUpdates);
                    }

                    // Determine next activity using transitions
                    var nextActivityId =
                        await DetermineNextActivityAsync(workflowSchema, currentActivity.Id, result,
                            workflowInstance);
                    if (!string.IsNullOrEmpty(nextActivityId))
                    {
                        var nextActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == nextActivityId);
                        if (nextActivity != null)
                        {
                            workflowInstance.SetCurrentActivity(nextActivity.Id);
                            activitiesToExecute.Enqueue(nextActivity);
                        }
                    }
                    else
                    {
                        // No next activity, workflow is completed
                        workflowInstance.UpdateStatus(WorkflowStatus.Completed);
                        _logger.LogInformation("Workflow instance {WorkflowInstanceId} completed", workflowInstance.Id);
                    }
                }
                else if (result.Status == ActivityResultStatus.Failed)
                {
                    activityExecution.Fail(result.ErrorMessage ?? "Activity execution failed");
                    workflowInstance.UpdateStatus(WorkflowStatus.Failed, result.ErrorMessage);
                }
                // For Pending status, activity remains in progress for external completion

                _logger.LogInformation(
                    "Executed activity {ActivityId} for workflow {WorkflowInstanceId} with status {Status}",
                    currentActivity.Id, workflowInstance.Id, result.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing activity {ActivityId} for workflow {WorkflowInstanceId}",
                    currentActivity.Id, workflowInstance.Id);

                workflowInstance.UpdateStatus(WorkflowStatus.Failed, ex.Message);
                throw;
            }
        }
    }
}

// New workflow events for MassTransit
public record WorkflowStarted
{
    public Guid WorkflowInstanceId { get; init; }
    public Guid WorkflowDefinitionId { get; init; }
    public string InstanceName { get; init; } = default!;
    public string StartedBy { get; init; } = default!;
    public DateTime StartedAt { get; init; }
    public string? CorrelationId { get; init; }
}

public record WorkflowActivityCompleted
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string CompletedBy { get; init; } = default!;
    public DateTime CompletedAt { get; init; }
    public Dictionary<string, object> OutputData { get; init; } = new();
    public string? Comments { get; init; }
}

public record WorkflowCancelled
{
    public Guid WorkflowInstanceId { get; init; }
    public string CancelledBy { get; init; } = default!;
    public DateTime CancelledAt { get; init; }
    public string Reason { get; init; } = default!;
}