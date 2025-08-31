namespace Assignment.Workflow.Config;

/// <summary>
/// Centralized constants for workflow engine configuration
/// Extracted from magic numbers to improve maintainability and security
/// </summary>
public static class WorkflowEngineConstants
{
    /// <summary>
    /// Expression evaluation timeout to prevent DoS attacks
    /// </summary>
    public static readonly TimeSpan ExpressionEvaluationTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum JSON size for workflow definitions to prevent memory exhaustion
    /// </summary>
    public const int MaxWorkflowDefinitionJsonSize = 1024 * 1024; // 1MB

    /// <summary>
    /// Maximum number of activities allowed in a workflow definition
    /// </summary>
    public const int MaxActivitiesPerWorkflow = 1000;

    /// <summary>
    /// Maximum number of transitions allowed in a workflow definition
    /// </summary>
    public const int MaxTransitionsPerWorkflow = 2000;

    /// <summary>
    /// Maximum expression length for security validation
    /// </summary>
    public const int MaxExpressionLength = 2000;

    /// <summary>
    /// Maximum token count in expressions to prevent DoS
    /// </summary>
    public const int MaxExpressionTokenCount = 500;

    /// <summary>
    /// Maximum expression nesting depth to prevent stack overflow
    /// </summary>
    public const int MaxExpressionDepth = 50;

    /// <summary>
    /// Maximum JSON deserialization depth for security
    /// </summary>
    public const int MaxJsonDeserializationDepth = 32;

    /// <summary>
    /// LRU cache size for compiled expressions
    /// </summary>
    public const int ExpressionCacheSize = 1000;
}