namespace Workflow.Workflow.Schema;

public class WorkflowSchema
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;
    public List<ActivityDefinition> Activities { get; set; } = new();
    public List<TransitionDefinition> Transitions { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public WorkflowMetadata Metadata { get; set; } = new();
    
    // Enhanced versioning support
    public WorkflowVersionInfo VersionInfo { get; set; } = new();
    
    // Expression capabilities
    public ExpressionCapabilities ExpressionCapabilities { get; set; } = new();
    
    // Validation schema
    public ValidationSchema ValidationSchema { get; set; } = new();
    
    // Runtime configuration
    public RuntimeConfiguration RuntimeConfiguration { get; set; } = new();
}

public class ActivityDefinition
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Dictionary<string, object> Properties { get; set; } = new();
    public ActivityPosition Position { get; set; } = new();
    public List<string> RequiredRoles { get; set; } = new();
    public TimeSpan? TimeoutDuration { get; set; }
    public bool IsStartActivity { get; set; }
    public bool IsEndActivity { get; set; }
    
    // Enhanced expression support
    public Dictionary<string, PropertyExpression> ExpressionProperties { get; set; } = new();
    
    // Activity metadata for design-time tooling
    public ActivityMetadata ActivityMetadata { get; set; } = new();
    
    // Conditional execution
    public string? ExecutionCondition { get; set; }
    
    // Error handling configuration
    public ErrorHandlingConfiguration ErrorHandling { get; set; } = new();
}

public class TransitionDefinition
{
    public string Id { get; set; } = default!;
    public string From { get; set; } = default!;
    public string To { get; set; } = default!;
    public string? Condition { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public TransitionType Type { get; set; } = TransitionType.Normal;
}

public class ActivityPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class WorkflowMetadata
{
    public string Author { get; set; } = default!;
    public DateTime CreatedDate { get; set; }
    public string Version { get; set; } = "1.0";
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}

public enum TransitionType
{
    Normal,
    Conditional,
    Exception,
    Timeout
}

public static class ActivityTypes
{
    // Human task activities
    public const string HumanTask = "HumanTask";
    public const string TaskActivity = "TaskActivity"; // Deprecated: Use HumanTask instead, kept for backward compatibility
    
    // Control flow activities
    public const string IfElseActivity = "IfElseActivity";
    public const string SwitchActivity = "SwitchActivity";
    public const string StartActivity = "StartActivity";
    public const string EndActivity = "EndActivity";
    public const string ForkActivity = "ForkActivity";
    public const string JoinActivity = "JoinActivity";
    
    // Automated activities
    public const string ServiceActivity = "ServiceActivity";
    public const string TimerActivity = "TimerActivity";
    public const string NotificationActivity = "NotificationActivity";
}

public static class AppraisalActivityTypes
{
    public const string RequestSubmission = "RequestSubmission";
    public const string AdminReview = "AdminReview";
    public const string StaffAssignment = "StaffAssignment";
    public const string AppraisalWork = "AppraisalWork";
    public const string CheckerReview = "CheckerReview";
    public const string VerifierReview = "VerifierReview";
    public const string CommitteeReview = "CommitteeReview";
}

public class ActivityTypeDefinition
{
    public string Type { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public List<ActivityPropertyDefinition> Properties { get; init; } = new();
    public string Icon { get; init; } = default!;
    public string Color { get; init; } = "#3b82f6";
}

public class ActivityPropertyDefinition
{
    public string Name { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string Type { get; init; } = default!; // string, number, boolean, array, object
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public string? Description { get; init; }
    public List<string>? Options { get; init; } // For select/enum types
}

// Enhanced schema classes for versioning and expressions

/// <summary>
/// Version information for workflow schema
/// </summary>
public class WorkflowVersionInfo
{
    public string SchemaVersion { get; set; } = "2.0";
    public Guid DefinitionId { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "Draft"; // Draft, Published, Deprecated
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public string? MinimumEngineVersion { get; set; }
    public List<string> RequiredCapabilities { get; set; } = new();
    public Dictionary<string, object> VersionMetadata { get; set; } = new();
}

/// <summary>
/// Expression capabilities and configuration
/// </summary>
public class ExpressionCapabilities
{
    public List<string> SupportedExpressionTypes { get; set; } = new() { "CSharp", "Literal" };
    public Dictionary<string, object> GlobalVariables { get; set; } = new();
    public List<string> AllowedNamespaces { get; set; } = new();
    public List<string> RestrictedFunctions { get; set; } = new();
    public TimeSpan ExpressionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableExpressionCaching { get; set; } = true;
}

/// <summary>
/// Validation schema for design-time checks
/// </summary>
public class ValidationSchema
{
    public List<ValidationRule> Rules { get; set; } = new();
    public Dictionary<string, ActivityTypeValidation> ActivityValidations { get; set; } = new();
    public List<string> RequiredActivities { get; set; } = new();
    public List<string> ForbiddenActivities { get; set; } = new();
    public int MinActivities { get; set; } = 1;
    public int MaxActivities { get; set; } = 1000;
}

/// <summary>
/// Runtime configuration for workflow execution
/// </summary>
public class RuntimeConfiguration
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromHours(24);
    public int MaxConcurrentInstances { get; set; } = -1; // -1 for unlimited
    public bool EnableTelemetry { get; set; } = true;
    public string ExecutionMode { get; set; } = "Standard"; // Standard, Debug, FastExecution
    public Dictionary<string, object> EngineSettings { get; set; } = new();
}

/// <summary>
/// Expression property that can be evaluated at runtime
/// </summary>
public class PropertyExpression
{
    public string Expression { get; set; } = default!;
    public string ExpressionType { get; set; } = "CSharp";
    public object? DefaultValue { get; set; }
    public bool Required { get; set; }
    public List<string> Dependencies { get; set; } = new(); // Variables this expression depends on
}

/// <summary>
/// Metadata for activities for design-time tooling
/// </summary>
public class ActivityMetadata
{
    public string Category { get; set; } = "General";
    public string Icon { get; set; } = "activity";
    public string Color { get; set; } = "#3b82f6";
    public bool IsDeprecated { get; set; }
    public string? DeprecationMessage { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> DesignerProperties { get; set; } = new();
}

/// <summary>
/// Error handling configuration for activities
/// </summary>
public class ErrorHandlingConfiguration
{
    public ErrorHandlingStrategy Strategy { get; set; } = ErrorHandlingStrategy.Fail;
    public int MaxRetryAttempts { get; set; } = 0;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public string? FallbackActivityId { get; set; }
    public Dictionary<string, object> ErrorHandlerProperties { get; set; } = new();
}

/// <summary>
/// Validation rule for workflow schemas
/// </summary>
public class ValidationRule
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public RuleSeverity Severity { get; set; } = RuleSeverity.Error;
    public string RuleExpression { get; set; } = default!; // C# expression that returns bool
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Validation configuration for specific activity types
/// </summary>
public class ActivityTypeValidation
{
    public List<string> RequiredProperties { get; set; } = new();
    public Dictionary<string, PropertyValidation> PropertyValidations { get; set; } = new();
    public List<ValidationRule> CustomRules { get; set; } = new();
}

/// <summary>
/// Validation for activity properties
/// </summary>
public class PropertyValidation
{
    public string DataType { get; set; } = "string";
    public bool Required { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public string? Pattern { get; set; } // Regex pattern
    public List<object>? AllowedValues { get; set; }
    public string? ValidationExpression { get; set; } // Custom validation expression
}

/// <summary>
/// Error handling strategies
/// </summary>
public enum ErrorHandlingStrategy
{
    Fail,           // Stop workflow execution
    Retry,          // Retry the activity
    Skip,           // Skip the activity and continue
    Fallback,       // Execute fallback activity
    Compensate      // Execute compensation logic
}

/// <summary>
/// Rule severity levels
/// </summary>
public enum RuleSeverity
{
    Info,
    Warning,
    Error,
    Critical
}