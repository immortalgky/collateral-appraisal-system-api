namespace Workflow.Workflow.Expressions;

/// <summary>
/// Context for expression evaluation containing variables and workflow state
/// </summary>
public class ExpressionContext
{
    /// <summary>
    /// Variables available in the expression scope
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>
    /// Current workflow instance ID
    /// </summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Current activity ID
    /// </summary>
    public string? CurrentActivityId { get; set; }

    /// <summary>
    /// Current user/executor
    /// </summary>
    public string? CurrentUser { get; set; }

    /// <summary>
    /// Execution timestamp
    /// </summary>
    public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Global functions and helpers available in expressions
    /// </summary>
    public Dictionary<string, Delegate> Functions { get; set; } = new();

    /// <summary>
    /// Workflow metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Add a variable to the context
    /// </summary>
    public void AddVariable(string name, object value)
    {
        Variables[name] = value;
    }

    /// <summary>
    /// Add a function to the context
    /// </summary>
    public void AddFunction(string name, Delegate function)
    {
        Functions[name] = function;
    }

    /// <summary>
    /// Create a context with default workflow functions
    /// </summary>
    public static ExpressionContext CreateDefault(Guid workflowInstanceId, string? currentUser = null)
    {
        var context = new ExpressionContext
        {
            WorkflowInstanceId = workflowInstanceId,
            CurrentUser = currentUser,
            ExecutionTime = DateTime.UtcNow
        };

        // Add common workflow functions
        context.AddFunction("Now", () => DateTime.UtcNow);
        context.AddFunction("Today", () => DateTime.UtcNow.Date);
        context.AddFunction("NewGuid", () => Guid.NewGuid());
        context.AddFunction("Random", () => new Random().NextDouble());

        return context;
    }
}