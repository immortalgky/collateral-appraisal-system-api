using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Workflow.Telemetry;

/// <summary>
/// Provides constants and semantic attributes for workflow telemetry operations.
/// </summary>
public static class WorkflowTelemetryConstants
{
    /// <summary>
    /// The name of the ActivitySource for workflow operations.
    /// </summary>
    public const string ActivitySourceName = "Workflow";

    /// <summary>
    /// The name of the Meter for workflow metrics.
    /// </summary>
    public const string MeterName = "Workflow";

    /// <summary>
    /// The version of the workflow telemetry instrumentation.
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// ActivitySource instance for creating workflow activities.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    /// <summary>
    /// Meter instance for creating workflow metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, Version);

    /// <summary>
    /// Semantic attribute keys for workflow operations following OpenTelemetry conventions.
    /// </summary>
    public static class SemanticAttributes
    {
        // Workflow Instance Attributes
        public const string WorkflowInstanceId = "workflow.instance.id";
        public const string WorkflowDefinitionId = "workflow.definition.id";
        public const string WorkflowDefinitionVersion = "workflow.definition.version";
        public const string WorkflowInstanceStatus = "workflow.instance.status";
        public const string WorkflowInstanceCorrelationId = "workflow.instance.correlation_id";

        // Workflow Activity Attributes
        public const string WorkflowActivityId = "workflow.activity.id";
        public const string WorkflowActivityName = "workflow.activity.name";
        public const string WorkflowActivityType = "workflow.activity.type";
        public const string WorkflowActivityStatus = "workflow.activity.status";
        public const string WorkflowActivityExecutionId = "workflow.activity.execution.id";
        public const string WorkflowActivityDuration = "workflow.activity.duration";
        public const string WorkflowActivityRetryCount = "workflow.activity.retry_count";

        // Workflow Action Attributes
        public const string WorkflowActionType = "workflow.action.type";
        public const string WorkflowActionName = "workflow.action.name";
        public const string WorkflowActionResult = "workflow.action.result";

        // External Call Attributes
        public const string ExternalCallUrl = "workflow.external_call.url";
        public const string ExternalCallMethod = "workflow.external_call.method";
        public const string ExternalCallStatusCode = "workflow.external_call.status_code";
        public const string ExternalCallDuration = "workflow.external_call.duration";

        // Workflow Engine Attributes
        public const string WorkflowEngineOperation = "workflow.engine.operation";
        public const string WorkflowEngineStep = "workflow.engine.step";
        public const string WorkflowEngineTransactionId = "workflow.engine.transaction_id";

        // Error and Exception Attributes
        public const string WorkflowErrorType = "workflow.error.type";
        public const string WorkflowErrorMessage = "workflow.error.message";
        public const string WorkflowErrorStackTrace = "workflow.error.stack_trace";

        // Workflow Persistence Attributes
        public const string WorkflowPersistenceOperation = "workflow.persistence.operation";
        public const string WorkflowBookmarkId = "workflow.bookmark.id";
        public const string WorkflowBookmarkName = "workflow.bookmark.name";

        // Assignment Attributes
        public const string AssignmentStrategy = "workflow.assignment.strategy";
        public const string AssigneeId = "workflow.assignee.id";
        public const string AssigneeGroup = "workflow.assignee.group";
    }

    /// <summary>
    /// Common log property names for structured logging.
    /// </summary>
    public static class LogProperties
    {
        public const string WorkflowInstanceId = "WorkflowInstanceId";
        public const string WorkflowDefinitionId = "WorkflowDefinitionId";
        public const string WorkflowActivityId = "WorkflowActivityId";
        public const string WorkflowActivityType = "WorkflowActivityType";
        public const string WorkflowStep = "WorkflowStep";
        public const string WorkflowOperation = "WorkflowOperation";
        public const string CorrelationId = "CorrelationId";
        public const string TransactionId = "TransactionId";
        public const string ExecutionDuration = "ExecutionDuration";
        public const string RetryCount = "RetryCount";
        public const string ErrorType = "ErrorType";
        public const string AssignmentStrategy = "AssignmentStrategy";
        public const string AssigneeId = "AssigneeId";
    }

    /// <summary>
    /// Activity names for different workflow operations.
    /// </summary>
    public static class ActivityNames
    {
        // Engine Operations
        public const string WorkflowExecution = "workflow.execute";
        public const string WorkflowStart = "workflow.start";
        public const string WorkflowComplete = "workflow.complete";
        public const string WorkflowSuspend = "workflow.suspend";
        public const string WorkflowResume = "workflow.resume";
        public const string WorkflowCancel = "workflow.cancel";

        // Activity Operations
        public const string ActivityExecution = "workflow.activity.execute";
        public const string ActivityStart = "workflow.activity.start";
        public const string ActivityComplete = "workflow.activity.complete";
        public const string ActivityError = "workflow.activity.error";

        // Action Operations
        public const string ActionExecution = "workflow.action.execute";
        public const string WebhookCall = "workflow.action.webhook";
        public const string ExternalCall = "workflow.external_call";

        // Persistence Operations
        public const string WorkflowPersist = "workflow.persist";
        public const string WorkflowLoad = "workflow.load";
        public const string BookmarkCreate = "workflow.bookmark.create";
        public const string BookmarkResume = "workflow.bookmark.resume";

        // Assignment Operations
        public const string AssignmentExecution = "workflow.assignment.execute";
        public const string AssigneeSelection = "workflow.assignee.select";
    }

    /// <summary>
    /// Meter names for different workflow metrics.
    /// </summary>
    public static class MeterNames
    {
        public const string WorkflowExecutions = "workflow_executions_total";
        public const string WorkflowDuration = "workflow_execution_duration";
        public const string ActivityExecutions = "workflow_activity_executions_total";
        public const string ActivityDuration = "workflow_activity_execution_duration";
        public const string WorkflowErrors = "workflow_errors_total";
        public const string WorkflowRetries = "workflow_retries_total";
        public const string ExternalCallDuration = "workflow_external_call_duration";
        public const string WorkflowActiveInstances = "workflow_active_instances";
        public const string BookmarkOperations = "workflow_bookmark_operations_total";
    }
}