using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Models;
using Assignment.AssigneeSelection.Core;
using Assignment.AssigneeSelection.Factories;
using Assignment.AssigneeSelection.Engine;
using Assignment.AssigneeSelection.Services;
using Assignment.Services.Configuration;
using Assignment.Workflow.Engine.Expression;
using Assignment.Workflow.Actions.Core;
using Assignment.Workflow.Services;

namespace Assignment.Workflow.Activities;

public class TaskActivity : WorkflowActivityBase
{
    private readonly IAssigneeSelectorFactory _assigneeSelectorFactory;
    private readonly ICascadingAssignmentEngine _cascadingEngine;
    private readonly ITaskConfigurationService _configurationService;
    private readonly ICustomAssignmentServiceFactory _customAssignmentServiceFactory;
    private readonly IWorkflowActionExecutor _actionExecutor;
    private readonly IWorkflowAuditService _auditService;
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<TaskActivity> _logger;

    public TaskActivity(
        IAssigneeSelectorFactory assigneeSelectorFactory,
        ICascadingAssignmentEngine cascadingEngine,
        ITaskConfigurationService configurationService,
        ICustomAssignmentServiceFactory customAssignmentServiceFactory,
        IWorkflowActionExecutor actionExecutor,
        IWorkflowAuditService auditService,
        ILogger<TaskActivity> logger)
    {
        _assigneeSelectorFactory = assigneeSelectorFactory;
        _cascadingEngine = cascadingEngine;
        _configurationService = configurationService;
        _customAssignmentServiceFactory = customAssignmentServiceFactory;
        _actionExecutor = actionExecutor;
        _auditService = auditService;
        _expressionEvaluator = new ExpressionEvaluator();
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.TaskActivity;
    public override string Name => "Task Activity";
    public override string Description => "Assigns a task to a user or role for completion using various strategies";

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assigneeRole = GetProperty<string>(context, "assigneeRole");

            var assignee = GetProperty<string>(context, "assignee");
            var assigneeGroup = GetProperty<string>(context, "assigneeGroup");
            var assignmentStrategies = GetProperty<List<string>>(context, "assignmentStrategies", ["RoundRobin"]);
            var revisitAssignmentStrategies = GetProperty<List<string>>(context, "revisitAssignmentStrategies", ["RoundRobin"]);
            var activityName = GetProperty<string>(context, "activityName", context.ActivityId);
            var workflowDefinitionId = GetProperty<string>(context, "workflowDefinitionId");

            // Try to get external configuration first
            var externalConfig = await _configurationService.GetConfigurationAsync(
                context.ActivityId, workflowDefinitionId, cancellationToken);

            // Execute onStart actions before assignment logic
            await ExecuteLifecycleActionsAsync(context, ActivityLifecycleEvent.OnStart, cancellationToken);

            // Step 0: Try custom assignment service first (highest priority)
            var customServiceName = GetProperty<string>(context, "customAssignmentService");
            if (!string.IsNullOrEmpty(customServiceName))
            {
                var customAssignmentResult = await TryCustomAssignmentServiceAsync(
                    customServiceName, context, cancellationToken);
                    
                if (customAssignmentResult != null)
                {
                    return customAssignmentResult;
                }
            }

            // Step 0.5: Try runtime overrides second (API-provided overrides)
            if (context.RuntimeOverrides != null && context.RuntimeOverrides.HasAssignmentOverride)
            {
                var runtimeOverrideResult = await ProcessRuntimeOverrideAsync(context, cancellationToken);
                if (runtimeOverrideResult != null)
                {
                    return runtimeOverrideResult;
                }
            }

            // Step 1: Try to find previous handler first (simplified logic)
            var previousOwnerSelector = _assigneeSelectorFactory.GetSelector(AssigneeSelectionStrategy.PreviousOwner);
            var previousOwnerContext = new AssignmentContext
            {
                ActivityName = activityName,
                Properties = new Dictionary<string, object>
                {
                    ["WorkflowInstanceId"] = context.WorkflowInstance.Id,
                    ["ActivityId"] = context.ActivityId
                }
            };

            var previousOwnerResult = await previousOwnerSelector.SelectAssigneeAsync(previousOwnerContext, cancellationToken);
            
            if (previousOwnerResult.IsSuccess && !string.IsNullOrEmpty(previousOwnerResult.AssigneeId))
            {
                _logger.LogInformation("Found previous handler for activity {ActivityId}: {AssigneeId}", 
                    context.ActivityId, previousOwnerResult.AssigneeId);
                
                // Assign to previous handler and return early
                SetActivityAssignee(context, previousOwnerResult.AssigneeId);

                // Log assignment change to audit service
                await _auditService.LogAssignmentChangeAsync(
                    context,
                    null, // no previous assignee
                    previousOwnerResult.AssigneeId,
                    "Found and assigned to previous handler",
                    AssignmentChangeType.InitialAssignment,
                    previousOwnerResult.Metadata,
                    cancellationToken);
                
                var previousOwnerOutput = new Dictionary<string, object>
                {
                    ["assigneeRole"] = assigneeRole ?? "",
                    ["assignee"] = previousOwnerResult.AssigneeId,
                    ["assignee_group"] = assigneeGroup ?? "",
                    ["activityName"] = activityName,
                    ["assignedTo"] = previousOwnerResult.AssigneeId,
                    ["assignmentMetadata"] = previousOwnerResult.Metadata ?? new Dictionary<string, object>(),
                    ["isPreviousHandler"] = true,
                    [$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "pending",
                    [$"{NormalizeActivityId(context.ActivityId)}_assignedTo"] = previousOwnerResult.AssigneeId,
                    [$"{NormalizeActivityId(context.ActivityId)}_assignmentMetadata"] = previousOwnerResult.Metadata ?? new Dictionary<string, object>()
                };
                
                return ActivityResult.Pending(previousOwnerOutput);
            }

            // Step 2: No previous handler found, use assignment strategies
            _logger.LogInformation("No previous handler found for activity {ActivityId}, using assignment strategies", 
                context.ActivityId);

            List<string> strategiesToUse;
            if (externalConfig != null)
            {
                // Use external configuration - just use PrimaryStrategies (no route-back complexity)
                strategiesToUse = externalConfig.PrimaryStrategies;

                // Override local properties with external configuration if available
                assignee = externalConfig.SpecificAssignee ?? assignee;
                assigneeGroup = externalConfig.AssigneeGroup ?? assigneeGroup;

                _logger.LogInformation(
                    "Using external configuration for activity {ActivityId}. Strategies: {Strategies}",
                    context.ActivityId, string.Join(", ", strategiesToUse));
            }
            else
            {
                // Use the workflow definition strategies
                strategiesToUse = assignmentStrategies;

                _logger.LogInformation("Using assignment strategies for activity {ActivityId}: {Strategies}",
                    context.ActivityId, string.Join(", ", strategiesToUse));
            }

            // Create assignment context
            var assignmentContext = new AssignmentContext
            {
                ActivityName = activityName,
                AssignmentStrategies = strategiesToUse,
                
                // TODO: Add support for multiple assignees by splitting the assignee string
                UserGroups = [assigneeGroup],
                UserCode = assignee,

                Properties = new Dictionary<string, object>
                {
                    ["WorkflowInstanceId"] = context.WorkflowInstance.Id,
                    ["ActivityId"] = context.ActivityId
                }
            };

            // NOTE: SupervisorId and ReplacementUserId configuration removed
            // Supervisor assignment now uses UserManagement mock data in SupervisorAssigneeSelector

            // Execute cascading assignment strategies
            var result = await _cascadingEngine.ExecuteAsync(assignmentContext, cancellationToken);

            // If primary strategies failed, try admin pool fallback
            if (!result.IsSuccess && externalConfig?.EscalateToAdminPool == true)
            {
                _logger.LogWarning("Primary assignment strategies failed for activity {ActivityId}. Attempting admin pool fallback",
                    context.ActivityId);

                result = await TryAdminPoolFallbackAsync(externalConfig, assignmentContext);
            }

            var outputData = new Dictionary<string, object>
            {
                ["assigneeRole"] = assigneeRole ?? "",
                ["assignee"] = assignee ?? "",
                ["assignee_group"] = assigneeGroup ?? "",
                ["activityName"] = activityName,
                ["isPreviousHandler"] = false, // Simplified: false since we handle previous handler separately above
                [$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "pending"
            };

            if (result.IsSuccess)
            {
                // Set the assignee immediately after a successful assignment
                SetActivityAssignee(context, result.AssigneeId);

                // Log assignment change to audit service
                await _auditService.LogAssignmentChangeAsync(
                    context,
                    null, // no previous assignee
                    result.AssigneeId,
                    $"Assigned using strategies: {string.Join(", ", strategiesToUse)}",
                    AssignmentChangeType.InitialAssignment,
                    result.Metadata,
                    cancellationToken);

                outputData["assignedTo"] = result.AssigneeId ?? "";
                outputData["assignmentMetadata"] = result.Metadata ?? new Dictionary<string, object>();
                outputData[$"{NormalizeActivityId(context.ActivityId)}_assignedTo"] = result.AssigneeId ?? "";
                outputData[$"{NormalizeActivityId(context.ActivityId)}_assignmentMetadata"] =
                    result.Metadata ?? new Dictionary<string, object>();

                _logger.LogInformation(
                    "Task assigned successfully for activity {ActivityId}. Assignee: {AssigneeId}. Assignment type: {AssignmentType}",
                    context.ActivityId, result.AssigneeId ?? "Group Assignment", "Strategy-based");
            }
            else
            {
                _logger.LogError("Assignment failed for activity {ActivityId}: {ErrorMessage}",
                    context.ActivityId, result.ErrorMessage);
                outputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "assignment_failed";
                outputData["assignmentError"] = result.ErrorMessage ?? "";
                outputData["assignmentMetadata"] = result.Metadata ?? new Dictionary<string, object>();
            }

            // Execute onComplete actions after successful assignment
            await ExecuteLifecycleActionsAsync(context, ActivityLifecycleEvent.OnComplete, cancellationToken);

            // Task activities return pending and wait for external completion via resume workflow
            return ActivityResult.Pending(outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task activity {ActivityId}", context.ActivityId);

            // Execute onError actions when task activity fails
            try
            {
                await ExecuteLifecycleActionsAsync(context, ActivityLifecycleEvent.OnError, cancellationToken);
            }
            catch (Exception actionEx)
            {
                _logger.LogError(actionEx, "Error executing onError actions for activity {ActivityId}", context.ActivityId);
            }

            var errorOutputData = new Dictionary<string, object>
            {
                [$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "failed",
                [$"{NormalizeActivityId(context.ActivityId)}_error"] = ex.Message
            };

            return ActivityResult.Failed($"Task activity failed: {ex.Message}", errorOutputData);
        }
    }

    protected override async Task<ActivityResult> ResumeActivityAsync(ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var outputData = new Dictionary<string, object>();

            // Process input mappings: map workflow variables to activity inputs
            var inputMappings =
                GetProperty<Dictionary<string, string>>(context, "inputMappings", new Dictionary<string, string>());
            var activityInputs = new Dictionary<string, object>();

            foreach (var mapping in inputMappings)
            {
                var activityInputName = mapping.Key;
                var workflowVariableName = mapping.Value;

                if (context.Variables.TryGetValue(workflowVariableName, out var value))
                {
                    activityInputs[activityInputName] = value;
                }
            }

            // Handle decision/action taken - primary use case for TaskActivity
            if (resumeInput.TryGetValue("decisionTaken", out var decisionTaken))
            {
                outputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = decisionTaken;
                outputData["decision"] = decisionTaken; // For transition evaluation
            }

            // Process all input data using inputMappings (unified approach)
            foreach (var kvp in resumeInput)
            {
                if (!IsReservedKey(kvp.Key))
                {
                    // Check if there's a specific mapping for this input
                    var mappingFound = false;
                    foreach (var mapping in inputMappings)
                    {
                        if (mapping.Key == kvp.Key)
                        {
                            // Use the mapped workflow variable name
                            var resolvedVariableName = mapping.Value.Replace("{activityId}", context.ActivityId);
                            outputData[resolvedVariableName] = kvp.Value;
                            mappingFound = true;
                            break;
                        }
                    }

                    // If no specific mapping found, use the activity ID prefix to avoid conflicts
                    if (!mappingFound)
                    {
                        outputData[$"{NormalizeActivityId(context.ActivityId)}_{kvp.Key}"] = kvp.Value;
                    }
                }
            }

            // Process output mappings: transform activity outputs to specific workflow variables
            var outputMappings =
                GetProperty<Dictionary<string, string>>(context, "outputMappings", new Dictionary<string, string>());
            foreach (var mapping in outputMappings)
            {
                var activityOutputName = mapping.Key;
                var workflowVariableName = mapping.Value;

                // Check if this output was generated (from calculations, processing, etc.)
                if (outputData.TryGetValue(activityOutputName, out var outputValue) ||
                    activityInputs.TryGetValue(activityOutputName, out outputValue) ||
                    resumeInput.TryGetValue(activityOutputName, out outputValue))
                {
                    var resolvedVariableName = workflowVariableName.Replace("{activityId}", context.ActivityId);
                    outputData[resolvedVariableName] = outputValue;
                }
            }

            // Default status update - mark as completed unless specified otherwise
            if (!outputData.ContainsKey($"{NormalizeActivityId(context.ActivityId)}_decisionTaken"))
            {
                outputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "completed";
            }

            // Evaluate decision conditions if configured (new conditional decision feature)
            var decisionConditions = GetProperty<Dictionary<string, string>>(context, "decisionConditions");
            if (decisionConditions != null && decisionConditions.Any())
            {
                var evaluatedDecision = EvaluateDecisionConditions(decisionConditions, outputData, context);
                if (!string.IsNullOrEmpty(evaluatedDecision))
                {
                    outputData["decision"] = evaluatedDecision; // Clean output for workflow routing
                    _logger.LogInformation("TaskActivity {ActivityId} evaluated decision conditions. Result: {Decision}", 
                        context.ActivityId, evaluatedDecision);
                }
            }

            _logger.LogInformation("TaskActivity {ActivityId} resumed with {InputCount} inputs, {OutputCount} outputs",
                context.ActivityId, activityInputs.Count, outputData.Count);

            return ActivityResult.Success(outputData: outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming TaskActivity {ActivityId}", context.ActivityId);

            // Execute onError actions when task resume fails
            try
            {
                await ExecuteLifecycleActionsAsync(context, ActivityLifecycleEvent.OnError, cancellationToken);
            }
            catch (Exception actionEx)
            {
                _logger.LogError(actionEx, "Error executing onError actions during TaskActivity resume for activity {ActivityId}", context.ActivityId);
            }

            var errorOutputData = new Dictionary<string, object>
            {
                [$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "resume_failed",
                [$"{NormalizeActivityId(context.ActivityId)}_error"] = ex.Message
            };

            return ActivityResult.Failed($"TaskActivity resume failed: {ex.Message}", errorOutputData);
        }
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var assigneeRole = GetProperty<string>(context, "assigneeRole");
        if (string.IsNullOrEmpty(assigneeRole))
        {
            errors.Add("AssigneeRole is required for TaskActivity");
        }

        var assignee = GetProperty<string>(context, "assignee");
        var assigneeGroup = GetProperty<string>(context, "assigneeGroup");
        var assignmentStrategies = GetProperty<List<string>>(context, "assignmentStrategies", ["Manual"]);

        // For Manual strategy, validate that either assignee or assignee_group is provided
        if (assignmentStrategies.Any(s => s.Equals("Manual", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrEmpty(assignee) && string.IsNullOrEmpty(assigneeGroup))
            {
                errors.Add("For Manual assignment strategy, either 'assignee' or 'assignee_group' must be specified");
            }
        }

        // Validate each strategy
        foreach (var strategy in assignmentStrategies)
        {
            try
            {
                AssignmentStrategyExtensions.FromString(strategy);
            }
            catch (ArgumentException)
            {
                errors.Add($"Invalid assignment strategy: {strategy}");
            }
        }

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        // For TaskActivity, we may not know the assignee yet, so use CurrentAssignee
        // The assignee will be updated after assignment logic runs
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            context.CurrentAssignee, // Will be updated after assignment
            context.Variables);
    }

    private Task<AssigneeSelectionResult> TryAdminPoolFallbackAsync(
        Assignment.Services.Configuration.Models.TaskAssignmentConfigurationDto config,
        AssignmentContext assignmentContext)
    {
        try
        {
            if (!string.IsNullOrEmpty(config.AdminPoolId))
            {
                // Assign to specific admin pool/group
                _logger.LogInformation("Attempting admin pool fallback for activity {ActivityId}. Admin Pool: {AdminPoolId}",
                    assignmentContext.ActivityName, config.AdminPoolId);

                return Task.FromResult(AssigneeSelectionResult.Success(config.AdminPoolId, new Dictionary<string, object>
                {
                    ["SelectionStrategy"] = "AdminPoolFallback",
                    ["AdminPoolId"] = config.AdminPoolId,
                    ["IsFallbackAssignment"] = true,
                    ["FallbackReason"] = "Primary assignment strategies failed"
                }));
            }
            else
            {
                // Default admin pool fallback - assign to a default admin group or queue
                _logger.LogInformation("Attempting default admin pool fallback for activity {ActivityId}",
                    assignmentContext.ActivityName);

                return Task.FromResult(AssigneeSelectionResult.Success("ADMIN_POOL", new Dictionary<string, object>
                {
                    ["SelectionStrategy"] = "AdminPoolFallback",
                    ["AdminPoolId"] = "ADMIN_POOL",
                    ["IsFallbackAssignment"] = true,
                    ["FallbackReason"] = "Primary assignment strategies failed"
                }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin pool fallback failed for activity {ActivityId}", assignmentContext.ActivityName);
            return Task.FromResult(AssigneeSelectionResult.Failure($"Admin pool fallback failed: {ex.Message}"));
        }
    }

    private static bool IsReservedKey(string key)
    {
        return key.Equals("decisionTaken", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("decision", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("comments", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("completedBy", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("variableUpdates", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Evaluates decision conditions using the elegant "output": "condition" syntax
    /// Returns the first matching condition's output decision
    /// </summary>
    private string EvaluateDecisionConditions(
        Dictionary<string, string> decisionConditions,
        Dictionary<string, object> currentData,
        ActivityContext context)
    {
        try
        {
            // Create combined variable context for expression evaluation
            var combinedVariables = new Dictionary<string, object>(context.Variables);
            
            // Add current activity output data to evaluation context
            foreach (var kvp in currentData)
            {
                combinedVariables[kvp.Key] = kvp.Value;
            }

            // Evaluate each condition in order until one matches
            foreach (var kvp in decisionConditions)
            {
                var outputDecision = kvp.Key;      // e.g. "approved", "rejected", "escalate"
                var condition = kvp.Value;         // e.g. "loanReview_decisionTaken == 'APP' && amount < 100000"

                try
                {
                    // Use expression evaluator to check condition
                    var conditionResult = _expressionEvaluator.EvaluateExpression(condition, combinedVariables);
                    
                    if (conditionResult)
                    {
                        _logger.LogDebug("Decision condition matched for TaskActivity {ActivityId}: '{OutputDecision}' <- '{Condition}'", 
                            context.ActivityId, outputDecision, condition);
                        return outputDecision;
                    }
                }
                catch (Exception conditionEx)
                {
                    _logger.LogWarning(conditionEx, "Failed to evaluate decision condition '{Condition}' for TaskActivity {ActivityId}. Skipping condition.",
                        condition, context.ActivityId);
                    // Continue to next condition instead of failing entire evaluation
                }
            }

            // No conditions matched - check if there's a default/fallback condition
            if (decisionConditions.ContainsKey("default"))
            {
                return "default";
            }

            _logger.LogDebug("No decision conditions matched for TaskActivity {ActivityId}. Using fallback decision.", context.ActivityId);
            return "unknown"; // Fallback decision
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating decision conditions for TaskActivity {ActivityId}", context.ActivityId);
            return "error"; // Error fallback decision
        }
    }

    /// <summary>
    /// Attempts to use a custom assignment service for task assignment
    /// </summary>
    /// <param name="serviceName">Name of the custom assignment service</param>
    /// <param name="context">Activity context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Activity result if custom assignment was successful, null to fall back to standard logic</returns>
    private async Task<ActivityResult?> TryCustomAssignmentServiceAsync(
        string serviceName, 
        ActivityContext context, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting custom assignment using service '{ServiceName}' for activity {ActivityId}", 
                serviceName, context.ActivityId);

            // Get the custom assignment service
            var customService = _customAssignmentServiceFactory.GetService(serviceName);
            if (customService == null)
            {
                _logger.LogWarning("Custom assignment service '{ServiceName}' not found. Available services: {AvailableServices}",
                    serviceName, string.Join(", ", _customAssignmentServiceFactory.GetAvailableServices()));
                return null;
            }

            // Call the custom service
            var customResult = await customService.GetAssignmentContextAsync(
                context.WorkflowInstance.Id.ToString(),
                context.ActivityId,
                context.Variables,
                cancellationToken);

            // Check if custom service wants to handle assignment
            if (!customResult.UseCustomAssignment)
            {
                _logger.LogDebug("Custom assignment service '{ServiceName}' declined to handle assignment: {Reason}",
                    serviceName, customResult.Reason);
                return null;
            }

            _logger.LogInformation("Custom assignment service '{ServiceName}' handling assignment: {Reason}",
                serviceName, customResult.Reason);

            // Process custom assignment result
            return await ProcessCustomAssignmentResult(customResult, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in custom assignment service '{ServiceName}' for activity {ActivityId}",
                serviceName, context.ActivityId);
            
            // Return null to fall back to standard assignment logic
            return null;
        }
    }

    /// <summary>
    /// Processes the result from a custom assignment service and creates the appropriate activity result
    /// </summary>
    /// <param name="customResult">Result from custom assignment service</param>
    /// <param name="context">Activity context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Activity result based on custom assignment</returns>
    private async Task<ActivityResult> ProcessCustomAssignmentResult(
        CustomAssignmentResult customResult,
        ActivityContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var assigneeRole = GetProperty<string>(context, "assigneeRole");
            var activityName = GetProperty<string>(context, "activityName", context.ActivityId);

            // Handle specific assignee assignment
            if (!string.IsNullOrEmpty(customResult.SpecificAssignee))
            {
                SetActivityAssignee(context, customResult.SpecificAssignee);
                
                var specificAssigneeOutput = CreateAssignmentOutput(
                    assigneeRole, customResult.SpecificAssignee, null, activityName, context,
                    customResult.Metadata, customResult.Reason, false);

                _logger.LogInformation("Custom assignment completed - assigned to specific user: {Assignee}",
                    customResult.SpecificAssignee);
                    
                return ActivityResult.Pending(specificAssigneeOutput);
            }

            // Handle specific group assignment
            if (!string.IsNullOrEmpty(customResult.SpecificGroup))
            {
                var groupAssignmentOutput = CreateAssignmentOutput(
                    assigneeRole, null, customResult.SpecificGroup, activityName, context,
                    customResult.Metadata, customResult.Reason, false);

                _logger.LogInformation("Custom assignment completed - assigned to group: {Group}",
                    customResult.SpecificGroup);
                    
                return ActivityResult.Pending(groupAssignmentOutput);
            }

            // Handle custom strategies
            if (customResult.CustomStrategies != null && customResult.CustomStrategies.Any())
            {
                var assignmentContext = new AssignmentContext
                {
                    ActivityName = activityName,
                    AssignmentStrategies = customResult.CustomStrategies,
                    UserGroups = !string.IsNullOrEmpty(customResult.SpecificGroup) 
                        ? new List<string> { customResult.SpecificGroup } 
                        : new List<string>(),
                    Properties = new Dictionary<string, object>
                    {
                        ["WorkflowInstanceId"] = context.WorkflowInstance.Id,
                        ["ActivityId"] = context.ActivityId
                    }
                };

                // Add custom properties to assignment context
                if (customResult.CustomProperties != null)
                {
                    foreach (var prop in customResult.CustomProperties)
                    {
                        assignmentContext.Properties[prop.Key] = prop.Value;
                    }
                }

                var strategyResult = await _cascadingEngine.ExecuteAsync(assignmentContext, cancellationToken);
                
                if (strategyResult.IsSuccess)
                {
                    SetActivityAssignee(context, strategyResult.AssigneeId);
                    
                    var strategyOutput = CreateAssignmentOutput(
                        assigneeRole, strategyResult.AssigneeId, null, activityName, context,
                        MergeMetadata(customResult.Metadata, strategyResult.Metadata), 
                        customResult.Reason, false);

                    _logger.LogInformation("Custom assignment completed using strategies {Strategies} - assigned to: {Assignee}",
                        string.Join(", ", customResult.CustomStrategies), strategyResult.AssigneeId);
                        
                    return ActivityResult.Pending(strategyOutput);
                }
                else
                {
                    _logger.LogError("Custom assignment strategies failed: {Error}", strategyResult.ErrorMessage);
                    
                    var failureOutput = CreateAssignmentOutput(
                        assigneeRole, null, null, activityName, context,
                        customResult.Metadata, $"Custom assignment failed: {strategyResult.ErrorMessage ?? "Unknown error"}", false);
                    failureOutput[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "assignment_failed";
                    failureOutput["assignmentError"] = strategyResult.ErrorMessage ?? "Unknown error";
                    
                    return ActivityResult.Failed($"Custom assignment failed: {strategyResult.ErrorMessage}", failureOutput);
                }
            }

            // If we get here, the custom service indicated it should handle assignment but didn't provide specific instructions
            _logger.LogWarning("Custom assignment service indicated it should handle assignment but provided no specific instructions");
            return ActivityResult.Failed("Custom assignment service error: no assignment instructions provided");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing custom assignment result for activity {ActivityId}", context.ActivityId);
            throw;
        }
    }

    /// <summary>
    /// Creates standardized assignment output dictionary
    /// </summary>
    private Dictionary<string, object> CreateAssignmentOutput(
        string? assigneeRole,
        string? assignee,
        string? assigneeGroup,
        string activityName,
        ActivityContext context,
        Dictionary<string, object>? metadata,
        string reason,
        bool isPreviousHandler)
    {
        return new Dictionary<string, object>
        {
            ["assigneeRole"] = assigneeRole ?? "",
            ["assignee"] = assignee ?? "",
            ["assignee_group"] = assigneeGroup ?? "",
            ["activityName"] = activityName,
            ["assignedTo"] = assignee ?? "",
            ["assignmentMetadata"] = metadata ?? new Dictionary<string, object>(),
            ["assignmentReason"] = reason,
            ["isPreviousHandler"] = isPreviousHandler,
            [$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "pending",
            [$"{NormalizeActivityId(context.ActivityId)}_assignedTo"] = assignee ?? "",
            [$"{NormalizeActivityId(context.ActivityId)}_assignmentMetadata"] = metadata ?? new Dictionary<string, object>(),
            [$"{NormalizeActivityId(context.ActivityId)}_assignmentReason"] = reason
        };
    }

    /// <summary>
    /// Merges metadata from multiple sources
    /// </summary>
    private Dictionary<string, object> MergeMetadata(
        Dictionary<string, object>? customMetadata,
        Dictionary<string, object>? strategyMetadata)
    {
        var merged = new Dictionary<string, object>();
        
        if (customMetadata != null)
        {
            foreach (var kvp in customMetadata)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        
        if (strategyMetadata != null)
        {
            foreach (var kvp in strategyMetadata)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        
        return merged;
    }

    /// <summary>
    /// Processes runtime assignment overrides provided via API calls
    /// </summary>
    /// <param name="context">Activity context containing runtime overrides</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Activity result if runtime override was processed, null to fall back to standard logic</returns>
    private async Task<ActivityResult?> ProcessRuntimeOverrideAsync(
        ActivityContext context, 
        CancellationToken cancellationToken)
    {
        try
        {
            var runtimeOverride = context.RuntimeOverrides!;
            var assigneeRole = GetProperty<string>(context, "assigneeRole");
            var activityName = GetProperty<string>(context, "activityName", context.ActivityId);

            _logger.LogInformation("Processing runtime assignment override for activity {ActivityId}. Override by: {OverrideBy}, Reason: {Reason}",
                context.ActivityId, runtimeOverride.OverrideBy, runtimeOverride.OverrideReason);

            // Handle specific assignee override
            if (!string.IsNullOrEmpty(runtimeOverride.RuntimeAssignee))
            {
                // Validate assignee has required role
                var validationResult = await ValidateAssigneeRole(runtimeOverride.RuntimeAssignee, assigneeRole, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogError("Runtime assignee override validation failed for {Assignee}: {Error}",
                        runtimeOverride.RuntimeAssignee, validationResult.ErrorMessage);
                    return ActivityResult.Failed($"Runtime assignee override invalid: {validationResult.ErrorMessage}");
                }

                SetActivityAssignee(context, runtimeOverride.RuntimeAssignee);
                
                var output = CreateAssignmentOutput(
                    assigneeRole, runtimeOverride.RuntimeAssignee, null, activityName, context,
                    new Dictionary<string, object> { ["OverrideBy"] = runtimeOverride.OverrideBy ?? "" },
                    runtimeOverride.OverrideReason ?? "Runtime assignment override", false);

                _logger.LogInformation("Runtime override completed - assigned to specific user: {Assignee}",
                    runtimeOverride.RuntimeAssignee);
                    
                return ActivityResult.Pending(output);
            }

            // Handle specific group override
            if (!string.IsNullOrEmpty(runtimeOverride.RuntimeAssigneeGroup))
            {
                var output = CreateAssignmentOutput(
                    assigneeRole, null, runtimeOverride.RuntimeAssigneeGroup, activityName, context,
                    new Dictionary<string, object> { ["OverrideBy"] = runtimeOverride.OverrideBy ?? "" },
                    runtimeOverride.OverrideReason ?? "Runtime group assignment override", false);

                _logger.LogInformation("Runtime override completed - assigned to group: {Group}",
                    runtimeOverride.RuntimeAssigneeGroup);
                    
                return ActivityResult.Pending(output);
            }

            // Handle custom strategies override
            if (runtimeOverride.RuntimeAssignmentStrategies != null && runtimeOverride.RuntimeAssignmentStrategies.Any())
            {
                var assignmentContext = new AssignmentContext
                {
                    ActivityName = activityName,
                    AssignmentStrategies = runtimeOverride.RuntimeAssignmentStrategies,
                    Properties = new Dictionary<string, object>
                    {
                        ["WorkflowInstanceId"] = context.WorkflowInstance.Id,
                        ["ActivityId"] = context.ActivityId
                    }
                };

                // Add runtime override properties to assignment context
                if (runtimeOverride.OverrideProperties != null)
                {
                    foreach (var prop in runtimeOverride.OverrideProperties)
                    {
                        assignmentContext.Properties[prop.Key] = prop.Value;
                    }
                }

                var strategyResult = await _cascadingEngine.ExecuteAsync(assignmentContext, cancellationToken);
                
                if (strategyResult.IsSuccess)
                {
                    SetActivityAssignee(context, strategyResult.AssigneeId);
                    
                    var metadata = MergeMetadata(
                        new Dictionary<string, object> { ["OverrideBy"] = runtimeOverride.OverrideBy ?? "" },
                        strategyResult.Metadata);
                    
                    var output = CreateAssignmentOutput(
                        assigneeRole, strategyResult.AssigneeId, null, activityName, context,
                        metadata, runtimeOverride.OverrideReason ?? "Runtime strategy override", false);

                    _logger.LogInformation("Runtime override completed using strategies {Strategies} - assigned to: {Assignee}",
                        string.Join(", ", runtimeOverride.RuntimeAssignmentStrategies), strategyResult.AssigneeId);
                        
                    return ActivityResult.Pending(output);
                }
                else
                {
                    _logger.LogError("Runtime override strategies failed: {Error}", strategyResult.ErrorMessage);
                    return ActivityResult.Failed($"Runtime assignment override failed: {strategyResult.ErrorMessage ?? "Unknown error"}");
                }
            }

            // If we get here, runtime override exists but has no valid assignment instructions
            _logger.LogWarning("Runtime override provided but contains no valid assignment instructions");
            return null; // Fall back to standard assignment logic
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing runtime assignment override for activity {ActivityId}", context.ActivityId);
            // Return null to fall back to standard assignment logic instead of failing completely
            return null;
        }
    }

    /// <summary>
    /// Validates that an assignee has the required role for the activity
    /// </summary>
    /// <param name="assigneeId">User to validate</param>
    /// <param name="requiredRole">Required role</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    private async Task<AssigneeValidationResult> ValidateAssigneeRole(
        string assigneeId, 
        string? requiredRole, 
        CancellationToken cancellationToken)
    {
        try
        {
            // If no role is required, any user is valid
            if (string.IsNullOrEmpty(requiredRole))
            {
                return new AssigneeValidationResult { IsValid = true };
            }

            // In a real implementation, this would check the user's roles via a user service
            // For now, we'll do basic validation and assume the caller has verified permissions
            if (string.IsNullOrEmpty(assigneeId))
            {
                return new AssigneeValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Assignee ID cannot be empty" 
                };
            }

            // Basic role validation - in production this would integrate with user management system
            if (!await ValidateAssigneeHasRequiredRole(assigneeId, requiredRole, cancellationToken))
            {
                return new AssigneeValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"User {assigneeId} does not have required role: {requiredRole}" 
                };
            }
            
            _logger.LogDebug("Role validation passed for {AssigneeId} with required role {RequiredRole}",
                assigneeId, requiredRole);
                
            return new AssigneeValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating assignee {AssigneeId} for role {RequiredRole}", assigneeId, requiredRole);
            return new AssigneeValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = $"Validation error: {ex.Message}" 
            };
        }
    }

    /// <summary>
    /// Executes lifecycle actions configured for the activity at specific lifecycle events
    /// </summary>
    /// <param name="context">Activity context</param>
    /// <param name="lifecycleEvent">Lifecycle event to execute actions for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ExecuteLifecycleActionsAsync(
        ActivityContext context, 
        ActivityLifecycleEvent lifecycleEvent, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get action configurations for this lifecycle event from activity properties
            var actionConfigurations = GetLifecycleActionConfigurations(context, lifecycleEvent);

            if (!actionConfigurations.Any())
            {
                _logger.LogDebug("No {LifecycleEvent} actions configured for activity {ActivityId}", 
                    lifecycleEvent, context.ActivityId);
                return;
            }

            _logger.LogInformation("Executing {ActionCount} {LifecycleEvent} actions for activity {ActivityId}",
                actionConfigurations.Count, lifecycleEvent, context.ActivityId);

            // Execute the actions
            var executionResult = await _actionExecutor.ExecuteActionsAsync(
                context, actionConfigurations, lifecycleEvent, cancellationToken);

            if (executionResult.IsSuccess)
            {
                _logger.LogInformation("Successfully executed {SuccessCount}/{TotalCount} {LifecycleEvent} actions for activity {ActivityId}",
                    executionResult.SuccessfulActions, executionResult.SuccessfulActions + executionResult.FailedActions + executionResult.SkippedActions, lifecycleEvent, context.ActivityId);
            }
            else
            {
                _logger.LogWarning("Some {LifecycleEvent} actions failed for activity {ActivityId}: {FailedCount}/{TotalCount} failed. Errors: {Errors}",
                    lifecycleEvent, context.ActivityId, executionResult.FailedActions, executionResult.SuccessfulActions + executionResult.FailedActions + executionResult.SkippedActions,
                    executionResult.ErrorSummary ?? "No error details");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {LifecycleEvent} actions for activity {ActivityId}", 
                lifecycleEvent, context.ActivityId);
            throw;
        }
    }

    /// <summary>
    /// Gets action configurations for a specific lifecycle event from activity properties
    /// </summary>
    /// <param name="context">Activity context</param>
    /// <param name="lifecycleEvent">Lifecycle event</param>
    /// <returns>List of action configurations</returns>
    private List<WorkflowActionConfiguration> GetLifecycleActionConfigurations(
        ActivityContext context, 
        ActivityLifecycleEvent lifecycleEvent)
    {
        var configurations = new List<WorkflowActionConfiguration>();

        try
        {
            // Get actions for specific lifecycle event
            var lifecyclePropertyName = $"actions.{lifecycleEvent.ToString().ToLowerInvariant()}";
            var lifecycleActions = GetProperty<List<WorkflowActionConfiguration>>(context, lifecyclePropertyName);
            
            if (lifecycleActions != null && lifecycleActions.Any())
            {
                configurations.AddRange(lifecycleActions);
                _logger.LogDebug("Found {ActionCount} {LifecycleEvent} actions in property '{PropertyName}'",
                    lifecycleActions.Count, lifecycleEvent, lifecyclePropertyName);
            }

            // Also support legacy/alternative property names for backward compatibility
            var alternativePropertyNames = GetAlternativeActionPropertyNames(lifecycleEvent);
            foreach (var propertyName in alternativePropertyNames)
            {
                var alternativeActions = GetProperty<List<WorkflowActionConfiguration>>(context, propertyName);
                if (alternativeActions != null && alternativeActions.Any())
                {
                    configurations.AddRange(alternativeActions);
                    _logger.LogDebug("Found {ActionCount} actions in alternative property '{PropertyName}' for {LifecycleEvent}",
                        alternativeActions.Count, propertyName, lifecycleEvent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting action configurations for {LifecycleEvent} from activity {ActivityId}",
                lifecycleEvent, context.ActivityId);
        }

        return configurations;
    }

    /// <summary>
    /// Gets alternative property names for lifecycle actions to support different configuration styles
    /// </summary>
    /// <param name="lifecycleEvent">Lifecycle event</param>
    /// <returns>Alternative property names</returns>
    private static string[] GetAlternativeActionPropertyNames(ActivityLifecycleEvent lifecycleEvent)
    {
        return lifecycleEvent switch
        {
            ActivityLifecycleEvent.OnStart => new[] { "onStartActions", "startActions", "initActions" },
            ActivityLifecycleEvent.OnComplete => new[] { "onCompleteActions", "completeActions", "finishActions" },
            ActivityLifecycleEvent.OnError => new[] { "onErrorActions", "errorActions", "failureActions" },
            ActivityLifecycleEvent.OnCancel => new[] { "onCancelActions", "cancelActions", "abortActions" },
            ActivityLifecycleEvent.OnTimeout => new[] { "onTimeoutActions", "timeoutActions" },
            _ => Array.Empty<string>()
        };
    }


    /// <summary>
    /// Validates that an assignee has the required role
    /// Basic implementation - should be enhanced with actual user management integration
    /// </summary>
    /// <param name="assigneeId">User to validate</param>
    /// <param name="requiredRole">Required role</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has required role</returns>
    private Task<bool> ValidateAssigneeHasRequiredRole(
        string assigneeId, 
        string requiredRole, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Basic validation patterns - this should be replaced with actual user management integration
            var roleValidationPatterns = new Dictionary<string, string[]>
            {
                ["appraiser"] = new[] { "appraiser", "senior-appraiser", "admin" },
                ["senior-appraiser"] = new[] { "senior-appraiser", "admin" },
                ["admin"] = new[] { "admin" },
                ["supervisor"] = new[] { "supervisor", "admin" },
                ["reviewer"] = new[] { "reviewer", "supervisor", "admin" }
            };

            // Extract role from email pattern (basic implementation)
            var normalizedRole = requiredRole.ToLowerInvariant();
            var userRole = ExtractUserRoleFromId(assigneeId);

            if (roleValidationPatterns.TryGetValue(normalizedRole, out var allowedRoles))
            {
                return Task.FromResult(allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase));
            }

            // If role is not in known patterns, assume valid (fail-open for unknown roles)
            _logger.LogWarning("Unknown role validation requested: {RequiredRole} for user {AssigneeId}. Allowing access.",
                requiredRole, assigneeId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in role validation for {AssigneeId} with role {RequiredRole}",
                assigneeId, requiredRole);
            return Task.FromResult(false); // Fail-closed on errors
        }
    }

    /// <summary>
    /// Extracts user role from user ID (basic implementation based on email patterns)
    /// In production, this would query the user management system
    /// </summary>
    /// <param name="assigneeId">User ID</param>
    /// <returns>Extracted role</returns>
    private static string ExtractUserRoleFromId(string assigneeId)
    {
        // Basic role extraction from email patterns
        var lowerAssigneeId = assigneeId.ToLowerInvariant();
        
        if (lowerAssigneeId.Contains("admin")) return "admin";
        if (lowerAssigneeId.Contains("supervisor")) return "supervisor";
        if (lowerAssigneeId.Contains("senior-appraiser")) return "senior-appraiser";
        if (lowerAssigneeId.Contains("appraiser")) return "appraiser";
        if (lowerAssigneeId.Contains("reviewer")) return "reviewer";
        
        // Default to basic user role
        return "user";
    }

    /// <summary>
    /// Result of assignee validation
    /// </summary>
    private class AssigneeValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}