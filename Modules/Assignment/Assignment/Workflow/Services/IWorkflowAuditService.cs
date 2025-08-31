using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Actions.Core;
using Assignment.Workflow.Models;

namespace Assignment.Workflow.Services;

/// <summary>
/// Enhanced audit service specifically for workflow activities and actions
/// Provides structured audit logging with rich context and compliance features
/// </summary>
public interface IWorkflowAuditService
{
    /// <summary>
    /// Logs workflow lifecycle events with structured data
    /// </summary>
    Task LogWorkflowEventAsync(
        Guid workflowInstanceId,
        WorkflowAuditEventType eventType,
        WorkflowAuditSeverity severity,
        string message,
        string? userId = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs activity lifecycle events with activity context
    /// </summary>
    Task LogActivityEventAsync(
        ActivityContext context,
        ActivityAuditEventType eventType,
        WorkflowAuditSeverity severity,
        string message,
        string? userId = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs action execution events with action context
    /// </summary>
    Task LogActionExecutionAsync(
        ActivityContext context,
        string actionType,
        string actionName,
        ActivityLifecycleEvent lifecycleEvent,
        ActionExecutionResult result,
        TimeSpan executionDuration,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs assignment change events with detailed tracking
    /// </summary>
    Task LogAssignmentChangeAsync(
        ActivityContext context,
        string? previousAssignee,
        string? newAssignee,
        string changeReason,
        AssignmentChangeType changeType,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs security and compliance events
    /// </summary>
    Task LogSecurityEventAsync(
        Guid workflowInstanceId,
        string? activityId,
        SecurityEventType eventType,
        string description,
        string? userId = null,
        Dictionary<string, object>? securityContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs performance metrics and diagnostics
    /// </summary>
    Task LogPerformanceMetricsAsync(
        Guid workflowInstanceId,
        string? activityId,
        string operation,
        TimeSpan duration,
        bool successful,
        Dictionary<string, object>? metrics = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates audit trail entries for compliance tracking
    /// </summary>
    Task CreateAuditTrailAsync(
        string entityType,
        string entityId,
        string action,
        string description,
        AuditTrailCategory category,
        string? userId = null,
        Dictionary<string, object>? auditData = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of workflow audit events
/// </summary>
public enum WorkflowAuditEventType
{
    WorkflowStarted,
    WorkflowCompleted,
    WorkflowFailed,
    WorkflowCancelled,
    WorkflowSuspended,
    WorkflowResumed,
    WorkflowTimeout,
    StateTransition,
    VariableUpdated,
    ConfigurationChanged
}

/// <summary>
/// Types of activity audit events
/// </summary>
public enum ActivityAuditEventType
{
    ActivityStarted,
    ActivityCompleted,
    ActivityFailed,
    ActivitySkipped,
    ActivityTimeout,
    AssignmentMade,
    AssignmentChanged,
    ActionExecuted,
    ActionFailed,
    ValidationFailed,
    CustomEvent
}

/// <summary>
/// Types of assignment changes
/// </summary>
public enum AssignmentChangeType
{
    InitialAssignment,
    Reassignment,
    RuntimeOverride,
    CustomServiceAssignment,
    AutomaticReassignment,
    ManualReassignment,
    EscalationAssignment
}

/// <summary>
/// Security event types for compliance
/// </summary>
public enum SecurityEventType
{
    UnauthorizedAccess,
    PermissionDenied,
    SensitiveDataAccessed,
    ConfigurationChanged,
    PrivilegedActionPerformed,
    AuditLogModified,
    SuspiciousActivity,
    ComplianceViolation
}

/// <summary>
/// Audit severity levels
/// </summary>
public enum WorkflowAuditSeverity
{
    Verbose = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    Security = 6
}

/// <summary>
/// Audit trail categories for compliance
/// </summary>
public enum AuditTrailCategory
{
    System,
    User,
    Administrative,
    Security,
    Compliance,
    Financial,
    Operational,
    Technical
}

/// <summary>
/// Structured audit event data model
/// </summary>
public class WorkflowAuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Guid WorkflowInstanceId { get; init; }
    public string? ActivityId { get; init; }
    public string EventType { get; init; } = default!;
    public WorkflowAuditSeverity Severity { get; init; }
    public string Message { get; init; } = default!;
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public Dictionary<string, object> Context { get; init; } = new();
    public Dictionary<string, object> AdditionalData { get; init; } = new();
    public string? CorrelationId { get; init; }
    public string? TraceId { get; init; }
    public string Source { get; init; } = "WorkflowEngine";
    public string Version { get; init; } = "1.0";
}