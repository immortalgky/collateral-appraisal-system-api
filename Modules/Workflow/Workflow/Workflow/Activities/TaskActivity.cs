using System.Text.Json;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Events;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Services.Configuration.Models;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Actions.Core;
using Workflow.Sla.Services;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Activities;

public class TaskActivity : WorkflowActivityBase
{
    private readonly IAssignmentPipeline _assignmentPipeline;
    private readonly ITaskConfigurationService _configurationService;
    private readonly ICustomAssignmentServiceFactory _customAssignmentServiceFactory;
    private readonly IWorkflowActionExecutor _actionExecutor;
    private readonly IWorkflowAuditService _auditService;
    private readonly ISlaCalculator _slaCalculator;
    private readonly IPublisher _publisher;
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<TaskActivity> _logger;

    public TaskActivity(
        IAssignmentPipeline assignmentPipeline,
        ITaskConfigurationService configurationService,
        ICustomAssignmentServiceFactory customAssignmentServiceFactory,
        IWorkflowActionExecutor actionExecutor,
        IWorkflowAuditService auditService,
        ISlaCalculator slaCalculator,
        IPublisher publisher,
        ILogger<TaskActivity> logger)
    {
        _assignmentPipeline = assignmentPipeline;
        _configurationService = configurationService;
        _customAssignmentServiceFactory = customAssignmentServiceFactory;
        _actionExecutor = actionExecutor;
        _auditService = auditService;
        _slaCalculator = slaCalculator;
        _publisher = publisher;
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
            var workflowDefinitionId = context.WorkflowInstance.WorkflowDefinitionId.ToString();
            var activityName = GetProperty(context, "activityName", context.ActivityId);
            var assigneeRole = GetProperty<string>(context, "assigneeRole");
            var assigneeGroup = GetProperty<string>(context, "assigneeGroup");

            // System task: skip assignment pipeline, assign to "SYSTEM" sentinel
            var taskType = GetProperty(context, "taskType", "human");
            if (taskType.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteSystemTaskAsync(context, activityName, assigneeRole, assigneeGroup, cancellationToken);
            }

            // Pre-pipeline Step 1: Try custom assignment service (highest priority)
            var customServiceName = GetProperty<string>(context, "customAssignmentService");
            if (!string.IsNullOrEmpty(customServiceName))
            {
                var customResult = await TryCustomAssignmentServiceAsync(
                    customServiceName, context, cancellationToken);
                if (customResult != null) return customResult;
            }

            // Pre-pipeline Step 2: Load external config (feed strategies into context if available)
            var externalConfig = await _configurationService.GetConfigurationAsync(
                context.ActivityId, workflowDefinitionId, cancellationToken);

            // Pipeline: delegates to AssignmentPipeline for team filtering, strategies, validation
            var assignmentResult = await _assignmentPipeline.AssignAsync(context, cancellationToken);

            // Post-pipeline fallback: admin pool if pipeline fails and escalation configured
            if (!assignmentResult.IsSuccess && externalConfig?.EscalateToAdminPool == true)
            {
                _logger.LogWarning(
                    "Pipeline assignment failed for activity {ActivityId}. Attempting admin pool fallback",
                    context.ActivityId);

                var fallbackResult = TryAdminPoolFallback(externalConfig);
                if (fallbackResult != null)
                    assignmentResult = fallbackResult;
            }

            if (!assignmentResult.IsSuccess)
            {
                _logger.LogError("Assignment failed for activity {ActivityId}: {Error}",
                    context.ActivityId, assignmentResult.ErrorMessage);
                return ActivityResult.Failed(assignmentResult.ErrorMessage ?? "Assignment failed");
            }

            // Capture prior human completer for completer notification.
            // LastCompletedBy is updated only on human completions and survives
            // intervening system activities (CurrentAssignee would be cleared/SYSTEM here).
            var previousAssignee = context.WorkflowInstance.LastCompletedBy;

            // Set assignee on workflow instance
            SetActivityAssignee(context, assignmentResult.AssigneeId);

            // Audit log
            await _auditService.LogAssignmentChangeAsync(
                context,
                null,
                assignmentResult.AssigneeId,
                $"Assigned via pipeline ({assignmentResult.Strategy})",
                AssignmentChangeType.InitialAssignment,
                assignmentResult.Metadata,
                cancellationToken);

            // Publish domain event — assignedType: "1" = specific person, "2" = pool
            var assignedType = assignmentResult.Metadata?.TryGetValue("AssignedType", out var metaType) == true
                ? metaType?.ToString() ?? "1"
                : string.IsNullOrEmpty(assignmentResult.AssigneeId) ? "2" : "1";
            await PublishTaskAssignedEventAsync(context, assignmentResult.AssigneeId, assignedType, cancellationToken, previousAssignee);

            var outputData = new Dictionary<string, object>
            {
                ["assigneeRole"] = assigneeRole ?? "",
                ["assignee"] = assignmentResult.AssigneeId ?? "",
                ["assignee_group"] = assigneeGroup ?? "",
                ["assignmentStrategy"] = assignmentResult.Strategy ?? "Pipeline",
                ["assignedTo"] = assignmentResult.AssigneeId ?? "",
                ["assignmentMetadata"] = assignmentResult.Metadata ?? new Dictionary<string, object>(),
                [$"{NormalizeActivityId(context.ActivityId)}_assignedTo"] = assignmentResult.AssigneeId ?? "",
                [$"{NormalizeActivityId(context.ActivityId)}_assignmentMetadata"] =
                    assignmentResult.Metadata ?? new Dictionary<string, object>()
            };

            _logger.LogInformation(
                "Task assigned for activity {ActivityId}. Assignee: {AssigneeId}. Strategy: {Strategy}",
                context.ActivityId, assignmentResult.AssigneeId, assignmentResult.Strategy);

            return ActivityResult.Pending(outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task activity {ActivityId}", context.ActivityId);
            return ActivityResult.Failed($"Task activity failed: {ex.Message}");
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
                    activityInputs[activityInputName] = value;
            }

            // Handle decision/action taken - primary use case for TaskActivity
            if (resumeInput.TryGetValue("decisionTaken", out var decisionTaken))
            {
                outputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = decisionTaken;
                outputData["decision"] = decisionTaken; // For transition evaluation
            }

            // Capture optional free-text comment so PublishTaskCompletedEventAsync can persist it
            // onto CompletedTask.Remark. `comments` is on the reserved-key denylist below, so the
            // generic input-passthrough loop will skip it — we have to handle it explicitly here.
            if (resumeInput.TryGetValue("comments", out var commentsValue))
            {
                outputData[$"{NormalizeActivityId(context.ActivityId)}_comments"] = commentsValue;
            }

            // Process all input data using inputMappings (unified approach)
            foreach (var kvp in resumeInput)
                if (!IsReservedKey(kvp.Key))
                {
                    // Check if there's a specific mapping for this input
                    var mappingFound = false;
                    foreach (var mapping in inputMappings)
                        if (mapping.Key == kvp.Key)
                        {
                            // Use the mapped workflow variable name
                            var resolvedVariableName = mapping.Value.Replace("{activityId}", context.ActivityId);
                            outputData[resolvedVariableName] = kvp.Value;
                            mappingFound = true;
                            break;
                        }

                    // If no specific mapping found, use the activity ID prefix to avoid conflicts
                    if (!mappingFound) outputData[$"{NormalizeActivityId(context.ActivityId)}_{kvp.Key}"] = kvp.Value;
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
                outputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = "completed";

            // Evaluate decision conditions if configured (new conditional decision feature)
            var decisionConditions = GetProperty<Dictionary<string, string>>(context, "decisionConditions");
            if (decisionConditions.Any())
            {
                var evaluatedDecision = EvaluateDecisionConditions(decisionConditions, outputData, context);
                if (!string.IsNullOrEmpty(evaluatedDecision))
                {
                    outputData["decision"] = evaluatedDecision; // Clean output for workflow routing
                    _logger.LogInformation(
                        "TaskActivity {ActivityId} evaluated decision conditions. Result: {Decision}",
                        context.ActivityId, evaluatedDecision);
                }
            }

            // Publish domain event to move PendingTask → CompletedTask
            await PublishTaskCompletedEventAsync(context, outputData, cancellationToken);

            _logger.LogInformation("TaskActivity {ActivityId} resumed with {InputCount} inputs, {OutputCount} outputs",
                context.ActivityId, activityInputs.Count, outputData.Count);

            return ActivityResult.Success(outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming TaskActivity {ActivityId}", context.ActivityId);

            // Execute onError actions when the task resume fails
            try
            {
                await ExecuteLifecycleActionsAsync(context, ActivityLifecycleEvent.OnError, cancellationToken);
            }
            catch (Exception actionEx)
            {
                _logger.LogError(actionEx,
                    "Error executing onError actions during TaskActivity resume for activity {ActivityId}",
                    context.ActivityId);
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

        var taskType = GetProperty(context, "taskType", "human");

        // System tasks skip assignment validation — no assignee role or strategy needed
        if (taskType.Equals("system", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Core.ValidationResult.Success());

        var assigneeRole = GetProperty<string>(context, "assigneeRole");
        if (string.IsNullOrEmpty(assigneeRole)) errors.Add("AssigneeRole is required for TaskActivity");

        var assignee = GetProperty<string>(context, "assignee");
        var assigneeGroup = GetProperty<string>(context, "assigneeGroup");
        var assignmentStrategies = GetProperty<List<string>>(context, "assignmentStrategies", ["Manual"]);

        // For Manual strategy, validate that either assignee or assignee_group is provided
        if (assignmentStrategies.Any(s => s.Equals("Manual", StringComparison.OrdinalIgnoreCase)))
            if (string.IsNullOrEmpty(assignee) && string.IsNullOrEmpty(assigneeGroup))
                errors.Add("For Manual assignment strategy, either 'assignee' or 'assignee_group' must be specified");

        // Validate each strategy
        foreach (var strategy in assignmentStrategies)
            try
            {
                AssignmentStrategyExtensions.FromString(strategy);
            }
            catch (ArgumentException)
            {
                errors.Add($"Invalid assignment strategy: {strategy}");
            }

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            context.CurrentAssignee,
            context.Variables);
    }

    // ── Pre-pipeline: Custom assignment service ──

    private async Task<ActivityResult?> TryCustomAssignmentServiceAsync(
        string serviceName,
        ActivityContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Attempting custom assignment using service '{ServiceName}' for activity {ActivityId}",
                serviceName, context.ActivityId);

            var customService = _customAssignmentServiceFactory.GetService(serviceName);
            if (customService == null)
            {
                _logger.LogWarning(
                    "Custom assignment service '{ServiceName}' not found. Available services: {AvailableServices}",
                    serviceName, string.Join(", ", _customAssignmentServiceFactory.GetAvailableServices()));
                return null;
            }

            var customResult = await customService.GetAssignmentContextAsync(
                context.WorkflowInstance.Id.ToString(),
                context.ActivityId,
                context.Variables,
                cancellationToken);

            if (!customResult.UseCustomAssignment)
            {
                _logger.LogDebug("Custom assignment service '{ServiceName}' declined to handle assignment: {Reason}",
                    serviceName, customResult.Reason);
                return null;
            }

            // If custom service provides a specific assignee, use it directly
            if (!string.IsNullOrEmpty(customResult.SpecificAssignee))
            {
                var prevAssignee = context.WorkflowInstance.CurrentAssignee;
                SetActivityAssignee(context, customResult.SpecificAssignee);

                var assigneeRole = GetProperty<string>(context, "assigneeRole");
                var assigneeGroup = GetProperty<string>(context, "assigneeGroup");

                await _auditService.LogAssignmentChangeAsync(
                    context, null, customResult.SpecificAssignee,
                    customResult.Reason ?? "Custom assignment to specific user",
                    AssignmentChangeType.InitialAssignment,
                    customResult.Metadata, cancellationToken);

                await PublishTaskAssignedEventAsync(context, customResult.SpecificAssignee, "1",
                    cancellationToken, prevAssignee);

                var outputData = new Dictionary<string, object>
                {
                    ["assigneeRole"] = assigneeRole ?? "",
                    ["assignee"] = customResult.SpecificAssignee,
                    ["assignee_group"] = assigneeGroup ?? "",
                    ["assignmentStrategy"] = "CustomService",
                    ["assignedTo"] = customResult.SpecificAssignee,
                    ["assignmentMetadata"] = customResult.Metadata ?? new Dictionary<string, object>(),
                    [$"{NormalizeActivityId(context.ActivityId)}_assignedTo"] = customResult.SpecificAssignee,
                    [$"{NormalizeActivityId(context.ActivityId)}_assignmentMetadata"] =
                        customResult.Metadata ?? new Dictionary<string, object>()
                };

                _logger.LogInformation("Custom assignment completed - assigned to: {Assignee}",
                    customResult.SpecificAssignee);

                return ActivityResult.Pending(outputData);
            }

            // For other custom results (group, strategies), fall through to pipeline
            _logger.LogDebug("Custom service provided no specific assignee, falling through to pipeline");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in custom assignment service '{ServiceName}' for activity {ActivityId}",
                serviceName, context.ActivityId);
            return null;
        }
    }

    // ── System task execution (external system callback) ──

    private const string SystemAssignee = "SYSTEM";

    private async Task<ActivityResult> ExecuteSystemTaskAsync(
        ActivityContext context,
        string activityName,
        string? assigneeRole,
        string? assigneeGroup,
        CancellationToken cancellationToken)
    {
        SetActivityAssignee(context, SystemAssignee);

        await _auditService.LogAssignmentChangeAsync(
            context, null, SystemAssignee,
            "System task — awaiting external trigger",
            AssignmentChangeType.InitialAssignment,
            new Dictionary<string, object> { ["taskType"] = "system" },
            cancellationToken);

        await PublishTaskAssignedEventAsync(context, SystemAssignee, "1", cancellationToken);

        var outputData = new Dictionary<string, object>
        {
            ["assigneeRole"] = assigneeRole ?? "",
            ["assignee"] = SystemAssignee,
            ["assignee_group"] = assigneeGroup ?? "",
            ["assignmentStrategy"] = "System",
            ["assignedTo"] = SystemAssignee,
            ["assignmentMetadata"] = new Dictionary<string, object> { ["taskType"] = "system" },
            [$"{NormalizeActivityId(context.ActivityId)}_assignedTo"] = SystemAssignee,
            [$"{NormalizeActivityId(context.ActivityId)}_assignmentMetadata"] =
                new Dictionary<string, object> { ["taskType"] = "system" }
        };

        _logger.LogInformation(
            "System task created for activity {ActivityId}. Awaiting external trigger.",
            context.ActivityId);

        return ActivityResult.Pending(outputData);
    }

    // ── Post-pipeline: Admin pool fallback ──

    private AssignmentResult? TryAdminPoolFallback(TaskAssignmentConfigurationDto config)
    {
        try
        {
            var adminPoolId = !string.IsNullOrEmpty(config.AdminPoolId) ? config.AdminPoolId : "ADMIN_POOL";

            _logger.LogInformation("Admin pool fallback - assigning to: {AdminPoolId}", adminPoolId);

            return new AssignmentResult
            {
                IsSuccess = true,
                AssigneeId = adminPoolId,
                Strategy = "AdminPoolFallback",
                Metadata = new Dictionary<string, object>
                {
                    ["AdminPoolId"] = adminPoolId,
                    ["IsFallbackAssignment"] = true,
                    ["FallbackReason"] = "Pipeline assignment strategies failed"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin pool fallback failed");
            return null;
        }
    }

    // ── Decision evaluation ──

    private static bool IsReservedKey(string key)
    {
        return key.Equals("decisionTaken", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("decision", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("comments", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("completedBy", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("variableUpdates", StringComparison.OrdinalIgnoreCase);
    }

    private string EvaluateDecisionConditions(
        Dictionary<string, string> decisionConditions,
        Dictionary<string, object> currentData,
        ActivityContext context)
    {
        try
        {
            var combinedVariables = new Dictionary<string, object>(context.Variables);
            foreach (var kvp in currentData) combinedVariables[kvp.Key] = kvp.Value;

            foreach (var kvp in decisionConditions)
            {
                var outputDecision = kvp.Key;
                var condition = kvp.Value;

                try
                {
                    var conditionResult = _expressionEvaluator.EvaluateExpression(condition, combinedVariables);
                    if (conditionResult)
                    {
                        _logger.LogDebug(
                            "Decision condition matched for TaskActivity {ActivityId}: '{OutputDecision}' <- '{Condition}'",
                            context.ActivityId, outputDecision, condition);
                        return outputDecision;
                    }
                }
                catch (Exception conditionEx)
                {
                    _logger.LogWarning(conditionEx,
                        "Failed to evaluate decision condition '{Condition}' for TaskActivity {ActivityId}",
                        condition, context.ActivityId);
                }
            }

            if (decisionConditions.ContainsKey("default")) return "default";

            _logger.LogDebug("No decision conditions matched for TaskActivity {ActivityId}", context.ActivityId);
            return "unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating decision conditions for TaskActivity {ActivityId}",
                context.ActivityId);
            return "error";
        }
    }

    // ── Lifecycle actions ──

    private async Task ExecuteLifecycleActionsAsync(
        ActivityContext context,
        ActivityLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var actionConfigurations = GetLifecycleActionConfigurations(context, lifecycleEvent);
            if (!actionConfigurations.Any()) return;

            _logger.LogInformation("Executing {ActionCount} {LifecycleEvent} actions for activity {ActivityId}",
                actionConfigurations.Count, lifecycleEvent, context.ActivityId);

            var executionResult = await _actionExecutor.ExecuteActionsAsync(
                context, actionConfigurations, lifecycleEvent, cancellationToken);

            if (!executionResult.IsSuccess)
                _logger.LogWarning(
                    "Some {LifecycleEvent} actions failed for activity {ActivityId}: {Errors}",
                    lifecycleEvent, context.ActivityId, executionResult.ErrorSummary ?? "No error details");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {LifecycleEvent} actions for activity {ActivityId}",
                lifecycleEvent, context.ActivityId);
            throw;
        }
    }

    private List<WorkflowActionConfiguration> GetLifecycleActionConfigurations(
        ActivityContext context,
        ActivityLifecycleEvent lifecycleEvent)
    {
        var configurations = new List<WorkflowActionConfiguration>();

        try
        {
            var lifecyclePropertyName = $"actions.{lifecycleEvent.ToString().ToLowerInvariant()}";
            var lifecycleActions = GetProperty<List<WorkflowActionConfiguration>>(context, lifecyclePropertyName);
            if (lifecycleActions != null && lifecycleActions.Any())
                configurations.AddRange(lifecycleActions);

            var alternativePropertyNames = GetAlternativeActionPropertyNames(lifecycleEvent);
            foreach (var propertyName in alternativePropertyNames)
            {
                var alternativeActions = GetProperty<List<WorkflowActionConfiguration>>(context, propertyName);
                if (alternativeActions != null && alternativeActions.Any())
                    configurations.AddRange(alternativeActions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error extracting action configurations for {LifecycleEvent} from activity {ActivityId}",
                lifecycleEvent, context.ActivityId);
        }

        return configurations;
    }

    private static string[] GetAlternativeActionPropertyNames(ActivityLifecycleEvent lifecycleEvent)
    {
        return lifecycleEvent switch
        {
            ActivityLifecycleEvent.OnStart => ["onStartActions", "startActions", "initActions"],
            ActivityLifecycleEvent.OnComplete => ["onCompleteActions", "completeActions", "finishActions"],
            ActivityLifecycleEvent.OnError => ["onErrorActions", "errorActions", "failureActions"],
            ActivityLifecycleEvent.OnCancel => ["onCancelActions", "cancelActions", "abortActions"],
            ActivityLifecycleEvent.OnTimeout => ["onTimeoutActions", "timeoutActions"],
            _ => Array.Empty<string>()
        };
    }

    // ── Event publishing ──

    private async Task PublishTaskAssignedEventAsync(
        ActivityContext context, string? assigneeId, string? assigneeRole,
        CancellationToken cancellationToken, string? completedBy = null)
    {
        var taskName = GetStringProperty(context.Properties, "activityName") ?? context.ActivityId;

        // Use CorrelationId if available, otherwise fall back to WorkflowInstance.Id
        var correlationGuid = !string.IsNullOrEmpty(context.WorkflowInstance.CorrelationId)
            && Guid.TryParse(context.WorkflowInstance.CorrelationId, out var parsed)
                ? parsed
                : context.WorkflowInstance.Id;

        // Calculate SLA deadline
        var companyId = context.WorkflowInstance.Variables.TryGetValue("assignedCompanyId", out var cid)
            && Guid.TryParse(cid?.ToString(), out var parsedCid) ? parsedCid : (Guid?)null;
        var loanType = context.WorkflowInstance.Variables.TryGetValue("loanType", out var lt)
            ? lt?.ToString() : null;
        // Parse timeoutDuration from activity properties (ISO 8601 duration, e.g., "PT72H")
        TimeSpan? defaultTimeout = null;
        if (context.Properties.TryGetValue("timeoutDuration", out var td))
        {
            var tdStr = td?.ToString();
            if (!string.IsNullOrEmpty(tdStr))
            {
                try { defaultTimeout = System.Xml.XmlConvert.ToTimeSpan(tdStr); }
                catch { /* ignore invalid format, SLA config table will be used instead */ }
            }
        }

        var dueAt = await _slaCalculator.CalculateActivityDueAtAsync(
            context.ActivityId,
            context.WorkflowInstance.WorkflowDefinitionId,
            companyId,
            loanType,
            DateTime.UtcNow,
            defaultTimeout,
            context.WorkflowInstance.WorkflowDueAt,
            cancellationToken);

        _logger.LogInformation(
            "Publishing TaskAssignedEvent for {ActivityId}: CorrelationId={CorrelationId}, TaskName={TaskName}, AssignedTo={AssignedTo}, DueAt={DueAt}",
            context.ActivityId, correlationGuid, taskName, assigneeId, dueAt);

        await _publisher.Publish(
            new TaskAssignedEvent(correlationGuid, taskName, assigneeId ?? "", assigneeRole ?? "",
                DateTime.UtcNow, context.WorkflowInstanceId, context.ActivityId, dueAt,
                context.WorkflowInstance.StartedBy, context.WorkflowInstance.Name,
                context.ActivityName, completedBy),
            cancellationToken);
    }

    private async Task PublishTaskCompletedEventAsync(
        ActivityContext context, Dictionary<string, object> outputData,
        CancellationToken cancellationToken)
    {
        var taskName = GetStringProperty(context.Properties, "activityName") ?? context.ActivityId;

        // Use CorrelationId if available, otherwise fall back to WorkflowInstance.Id
        var correlationGuid = !string.IsNullOrEmpty(context.WorkflowInstance.CorrelationId)
            && Guid.TryParse(context.WorkflowInstance.CorrelationId, out var parsed)
                ? parsed
                : context.WorkflowInstance.Id;

        var activityKey = $"{NormalizeActivityId(context.ActivityId)}_decisionTaken";
        var actionTaken = outputData.TryGetValue(activityKey, out var decisionTaken)
            ? decisionTaken?.ToString() ?? "Completed"
            : "Completed";

        var commentsKey = $"{NormalizeActivityId(context.ActivityId)}_comments";
        var remark = outputData.TryGetValue(commentsKey, out var commentsValue)
            ? commentsValue?.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(remark))
        {
            remark = null;
        }

        // Pass CompletedBy for pool task implicit assignment
        var completedBy = context.WorkflowInstance.CurrentAssignee;

        await _publisher.Publish(
            new TaskCompletedDomainEvent(correlationGuid, taskName, actionTaken, DateTime.UtcNow, completedBy,
                context.WorkflowInstance.Name, remark),
            cancellationToken);
    }

    private static string? GetStringProperty(Dictionary<string, object>? properties, string key)
    {
        if (properties is null || !properties.TryGetValue(key, out var value) || value is null)
            return null;

        return value is JsonElement je ? je.GetString() : value.ToString();
    }
}
