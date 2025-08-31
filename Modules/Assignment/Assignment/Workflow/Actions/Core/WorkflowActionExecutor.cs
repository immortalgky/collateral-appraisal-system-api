using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Engine.Expression;
using Assignment.Workflow.Services;
using Assignment.Workflow.Resilience;

namespace Assignment.Workflow.Actions.Core;

/// <summary>
/// Concrete implementation of workflow action executor
/// Manages registration, validation, and execution of workflow actions
/// </summary>
public class WorkflowActionExecutor : IWorkflowActionExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IWorkflowAuditService _auditService;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly ILogger<WorkflowActionExecutor> _logger;
    private readonly Dictionary<string, Type> _registeredActions = new();

    public WorkflowActionExecutor(
        IServiceProvider serviceProvider,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IWorkflowAuditService auditService,
        IWorkflowResilienceService resilienceService,
        ILogger<WorkflowActionExecutor> logger)
    {
        _serviceProvider = serviceProvider;
        _expressionEvaluator = expressionEvaluator;
        _auditService = auditService;
        _resilienceService = resilienceService;
        _logger = logger;
    }

    public async Task<ActionBatchExecutionResult> ExecuteActionsAsync(
        ActivityContext context,
        List<WorkflowActionConfiguration> actionConfigurations,
        ActivityLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default)
    {
        if (!actionConfigurations.Any())
        {
            _logger.LogDebug("No actions configured for lifecycle event {LifecycleEvent} on activity {ActivityId}",
                lifecycleEvent, context.ActivityId);
            return ActionBatchExecutionResult.FromResults(new List<ActionExecutionResult>(), TimeSpan.Zero);
        }

        _logger.LogInformation("Executing {ActionCount} actions for lifecycle event {LifecycleEvent} on activity {ActivityId}",
            actionConfigurations.Count, lifecycleEvent, context.ActivityId);

        // Log batch execution start
        await _auditService.LogActivityEventAsync(
            context,
            ActivityAuditEventType.ActionExecuted,
            WorkflowAuditSeverity.Information,
            $"Starting execution of {actionConfigurations.Count} actions for {lifecycleEvent}",
            context.CurrentAssignee,
            new Dictionary<string, object>
            {
                ["actionCount"] = actionConfigurations.Count,
                ["lifecycleEvent"] = lifecycleEvent.ToString(),
                ["actionTypes"] = actionConfigurations.Select(a => a.ActionType).ToList()
            });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<ActionExecutionResult>();
        var skippedCount = 0;

        // Sort actions by priority
        var sortedActions = actionConfigurations.OrderBy(a => a.Priority).ToList();

        foreach (var actionConfig in sortedActions)
        {
            try
            {
                // Check condition if specified
                if (!string.IsNullOrEmpty(actionConfig.Condition))
                {
                    var conditionResult = await EvaluateConditionAsync(actionConfig.Condition, context, cancellationToken);
                    if (!conditionResult)
                    {
                        _logger.LogDebug("Skipping action '{ActionType}' - condition '{Condition}' evaluated to false",
                            actionConfig.ActionType, actionConfig.Condition);
                        skippedCount++;
                        continue;
                    }
                }

                var actionStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Execute action with resilience patterns
                var operationName = $"Action.{actionConfig.ActionType}";
                var result = await _resilienceService.ExecuteWithResilienceAsync(
                    operationName,
                    async ct => await ExecuteActionAsync(context, actionConfig, ct),
                    GetResiliencePolicyForAction(actionConfig),
                    cancellationToken);
                    
                actionStopwatch.Stop();
                
                results.Add(result);

                // Log individual action execution to audit service
                await _auditService.LogActionExecutionAsync(
                    context,
                    actionConfig.ActionType,
                    actionConfig.ActionType, // Could be enhanced with a Name property
                    lifecycleEvent,
                    result,
                    actionStopwatch.Elapsed,
                    context.CurrentAssignee);

                // Stop execution if action failed and ContinueOnFailure is false
                if (!result.IsSuccess && !actionConfig.ContinueOnFailure)
                {
                    _logger.LogWarning("Stopping action execution - action '{ActionType}' failed and ContinueOnFailure is false",
                        actionConfig.ActionType);

                    // Log early termination
                    await _auditService.LogActivityEventAsync(
                        context,
                        ActivityAuditEventType.ActionFailed,
                        WorkflowAuditSeverity.Warning,
                        $"Action execution terminated early due to failure in action '{actionConfig.ActionType}'",
                        context.CurrentAssignee,
                        new Dictionary<string, object>
                        {
                            ["terminatedActionType"] = actionConfig.ActionType,
                            ["remainingActionCount"] = sortedActions.Count - (results.Count + skippedCount),
                            ["errorMessage"] = result.ErrorMessage ?? "Unknown error"
                        });
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing action '{ActionType}' for activity {ActivityId}",
                    actionConfig.ActionType, context.ActivityId);

                var errorResult = ActionExecutionResult.Failed($"Unexpected error: {ex.Message}");
                results.Add(errorResult);

                // Log unexpected error to audit service
                await _auditService.LogActivityEventAsync(
                    context,
                    ActivityAuditEventType.ActionFailed,
                    WorkflowAuditSeverity.Error,
                    $"Unexpected error in action '{actionConfig.ActionType}': {ex.Message}",
                    context.CurrentAssignee,
                    new Dictionary<string, object>
                    {
                        ["actionType"] = actionConfig.ActionType,
                        ["exceptionType"] = ex.GetType().Name,
                        ["stackTrace"] = ex.StackTrace ?? "",
                        ["continueOnFailure"] = actionConfig.ContinueOnFailure
                    });

                if (!actionConfig.ContinueOnFailure)
                {
                    break;
                }
            }
        }

        stopwatch.Stop();

        var batchResult = ActionBatchExecutionResult.FromResults(results, stopwatch.Elapsed);
        batchResult = new ActionBatchExecutionResult
        {
            IsSuccess = batchResult.IsSuccess,
            SuccessfulActions = batchResult.SuccessfulActions,
            FailedActions = batchResult.FailedActions,
            SkippedActions = skippedCount,
            Results = batchResult.Results,
            AggregatedOutputData = batchResult.AggregatedOutputData,
            TotalExecutionDuration = batchResult.TotalExecutionDuration,
            ErrorSummary = batchResult.ErrorSummary
        };

        _logger.LogInformation("Completed action batch execution for activity {ActivityId}: {Successful} successful, {Failed} failed, {Skipped} skipped",
            context.ActivityId, batchResult.SuccessfulActions, batchResult.FailedActions, batchResult.SkippedActions);

        // Log batch execution completion with comprehensive summary
        var batchSeverity = batchResult.FailedActions > 0 ? WorkflowAuditSeverity.Warning : WorkflowAuditSeverity.Information;
        await _auditService.LogActivityEventAsync(
            context,
            ActivityAuditEventType.ActionExecuted,
            batchSeverity,
            $"Completed action batch for {lifecycleEvent}: {batchResult.SuccessfulActions} successful, {batchResult.FailedActions} failed, {batchResult.SkippedActions} skipped",
            context.CurrentAssignee,
            new Dictionary<string, object>
            {
                ["batchExecutionSummary"] = new Dictionary<string, object>
                {
                    ["totalActions"] = actionConfigurations.Count,
                    ["successfulActions"] = batchResult.SuccessfulActions,
                    ["failedActions"] = batchResult.FailedActions,
                    ["skippedActions"] = batchResult.SkippedActions,
                    ["totalExecutionDuration"] = batchResult.TotalExecutionDuration.TotalMilliseconds,
                    ["lifecycleEvent"] = lifecycleEvent.ToString(),
                    ["batchSuccess"] = batchResult.IsSuccess
                },
                ["actionResults"] = batchResult.Results.Select(r => new
                {
                    success = r.IsSuccess,
                    message = r.ResultMessage ?? r.ErrorMessage,
                    duration = r.ExecutionDuration.TotalMilliseconds
                }).ToList()
            });

        // Log performance metrics for the batch execution
        await _auditService.LogPerformanceMetricsAsync(
            context.WorkflowInstanceId,
            context.ActivityId,
            $"ActionBatch.{lifecycleEvent}",
            batchResult.TotalExecutionDuration,
            batchResult.IsSuccess,
            new Dictionary<string, object>
            {
                ["actionCount"] = actionConfigurations.Count,
                ["successfulActions"] = batchResult.SuccessfulActions,
                ["failedActions"] = batchResult.FailedActions
            });

        return batchResult;
    }

    public async Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        WorkflowActionConfiguration actionConfiguration,
        CancellationToken cancellationToken = default)
    {
        var actionType = actionConfiguration.ActionType;
        
        if (!_registeredActions.TryGetValue(actionType, out var actionImplementationType))
        {
            var errorMessage = $"Action type '{actionType}' is not registered. Available types: {string.Join(", ", _registeredActions.Keys)}";
            _logger.LogError("Action type '{ActionType}' not found", actionType);
            return ActionExecutionResult.Failed(errorMessage);
        }

        try
        {
            var actionInstance = (IWorkflowAction)_serviceProvider.GetRequiredService(actionImplementationType);
            return await actionInstance.ExecuteAsync(context, actionConfiguration.Parameters, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or execute action instance for type '{ActionType}'", actionType);
            return ActionExecutionResult.Failed($"Failed to execute action: {ex.Message}");
        }
    }

    public async Task<ActionBatchValidationResult> ValidateActionsAsync(
        List<WorkflowActionConfiguration> actionConfigurations,
        CancellationToken cancellationToken = default)
    {
        var validationResults = new List<ActionValidationResult>();

        foreach (var actionConfig in actionConfigurations)
        {
            var result = await ValidateActionConfigurationAsync(actionConfig, cancellationToken);
            validationResults.Add(result);
        }

        return ActionBatchValidationResult.FromResults(validationResults);
    }

    public IEnumerable<string> GetAvailableActionTypes()
    {
        return _registeredActions.Keys.OrderBy(x => x);
    }

    public ActionTypeInfo? GetActionTypeInfo(string actionType)
    {
        if (!_registeredActions.TryGetValue(actionType, out var actionImplementationType))
        {
            return null;
        }

        try
        {
            var actionInstance = (IWorkflowAction)_serviceProvider.GetRequiredService(actionImplementationType);
            
            return new ActionTypeInfo
            {
                ActionType = actionInstance.ActionType,
                Name = actionInstance.Name,
                Description = actionInstance.Description,
                // TODO: Could be enhanced to provide parameter info via reflection or attributes
                RequiredParameters = new List<ActionParameterInfo>(),
                OptionalParameters = new List<ActionParameterInfo>(),
                SampleConfigurations = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get action type info for '{ActionType}'", actionType);
            return null;
        }
    }

    /// <summary>
    /// Registers an action type with the executor
    /// </summary>
    /// <param name="actionType">Action type identifier</param>
    /// <param name="implementationType">Implementation type that implements IWorkflowAction</param>
    public void RegisterAction(string actionType, Type implementationType)
    {
        if (!typeof(IWorkflowAction).IsAssignableFrom(implementationType))
        {
            throw new ArgumentException($"Type {implementationType.Name} does not implement IWorkflowAction");
        }

        _registeredActions[actionType] = implementationType;
        _logger.LogDebug("Registered workflow action '{ActionType}' with implementation {ImplementationType}",
            actionType, implementationType.Name);
    }

    /// <summary>
    /// Registers multiple action types from the service collection
    /// </summary>
    public void RegisterActionsFromServiceCollection()
    {
        // This would typically be called during startup to auto-register all IWorkflowAction implementations
        _logger.LogInformation("Auto-registering workflow actions from service collection...");
        
        // Implementation would scan for all IWorkflowAction implementations in the service collection
        // For now, we'll register them manually in the module configuration
    }

    private async Task<ActionValidationResult> ValidateActionConfigurationAsync(
        WorkflowActionConfiguration actionConfiguration,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate action type exists
        if (string.IsNullOrEmpty(actionConfiguration.ActionType))
        {
            errors.Add("ActionType is required");
        }
        else if (!_registeredActions.ContainsKey(actionConfiguration.ActionType))
        {
            errors.Add($"Action type '{actionConfiguration.ActionType}' is not registered");
        }

        // Validate condition syntax if provided
        if (!string.IsNullOrEmpty(actionConfiguration.Condition))
        {
            try
            {
                // Basic validation of condition syntax
                // In a real implementation, you'd validate against the expression evaluator
                if (!actionConfiguration.Condition.Contains("${") && !actionConfiguration.Condition.Contains("==") && 
                    !actionConfiguration.Condition.Contains("!=") && !bool.TryParse(actionConfiguration.Condition, out _))
                {
                    warnings.Add($"Condition '{actionConfiguration.Condition}' may not be a valid expression");
                }
            }
            catch (Exception)
            {
                errors.Add($"Invalid condition syntax: '{actionConfiguration.Condition}'");
            }
        }

        // Validate action-specific parameters if action type is valid
        if (errors.Count == 0 && _registeredActions.TryGetValue(actionConfiguration.ActionType, out var actionType))
        {
            try
            {
                var actionInstance = (IWorkflowAction)_serviceProvider.GetRequiredService(actionType);
                var actionValidation = await actionInstance.ValidateAsync(actionConfiguration.Parameters, cancellationToken);
                
                if (!actionValidation.IsValid)
                {
                    errors.AddRange(actionValidation.Errors);
                }
                warnings.AddRange(actionValidation.Warnings);
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not validate action parameters: {ex.Message}");
            }
        }

        return errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings);
    }

    private async Task<bool> EvaluateConditionAsync(
        string condition,
        ActivityContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use the workflow expression evaluator to evaluate the condition
            var variables = context.Variables
                .Concat(context.Properties)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Add workflow context variables
            variables["workflow.instanceId"] = context.WorkflowInstanceId.ToString();
            variables["workflow.activityId"] = context.ActivityId;
            variables["workflow.assignee"] = context.CurrentAssignee ?? "";

            return await _expressionEvaluator.EvaluateBooleanAsync(condition, variables, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate condition '{Condition}' for activity {ActivityId}. Defaulting to true.",
                condition, context.ActivityId);
            
            // Default to true if condition evaluation fails
            return true;
        }
    }

    /// <summary>
    /// Determines the appropriate resilience policy for an action based on its type and configuration
    /// </summary>
    private ResiliencePolicy GetResiliencePolicyForAction(WorkflowActionConfiguration actionConfig)
    {
        // Different action types get different resilience policies
        return actionConfig.ActionType switch
        {
            "CallWebhook" => ResiliencePolicy.Aggressive, // External HTTP calls need aggressive resilience
            "PublishEvent" => ResiliencePolicy.Default, // Event publishing needs standard resilience
            "SendNotification" => ResiliencePolicy.Default, // Notifications need standard resilience
            "CreateAuditEntry" => ResiliencePolicy.Lenient, // Audit entries are less critical
            "UpdateEntityStatus" => ResiliencePolicy.Default, // Entity updates need standard resilience
            "SetWorkflowVariable" => ResiliencePolicy.Lenient, // Variable updates are fast operations
            "ConditionalAction" => ResiliencePolicy.Default, // Conditional logic needs standard resilience
            _ => ResiliencePolicy.Default // Default for unknown action types
        };
    }
}