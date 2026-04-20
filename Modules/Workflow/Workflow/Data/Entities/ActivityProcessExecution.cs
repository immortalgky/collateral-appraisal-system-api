namespace Workflow.Data.Entities;

/// <summary>
/// Outcome of a single step execution within a pipeline run.
/// </summary>
public enum StepOutcome : byte
{
    Passed = 0,
    Failed = 1,
    Skipped = 2,
    Errored = 3
}

/// <summary>
/// Reason a step was skipped without executing.
/// </summary>
public enum SkipReason : byte
{
    /// <summary>RunIfExpression evaluated to false.</summary>
    RunIfFalse = 0,
    /// <summary>Step is in the Action phase but Validation phase had failures.</summary>
    ValidationPhaseFailed = 1,
    /// <summary>RunIfExpression threw or returned non-boolean — treated as error, step skipped.</summary>
    ExpressionError = 2
}

/// <summary>
/// Per-step execution trace for one pipeline run.
/// Written via a dedicated sink connection so trace rows survive outer transaction rollbacks.
/// </summary>
public class ActivityProcessExecution
{
    public Guid Id { get; private set; }

    /// <summary>Workflow instance this pipeline run belongs to.</summary>
    public Guid WorkflowInstanceId { get; private set; }

    /// <summary>The workflow activity execution record that triggered this pipeline run.</summary>
    public Guid WorkflowActivityExecutionId { get; private set; }

    /// <summary>FK to the config row at time of execution (nullable: config may be deleted later).</summary>
    public Guid? ConfigurationId { get; private set; }

    /// <summary>Snapshot of the config row's Version at execution time.</summary>
    public int ConfigurationVersion { get; private set; }

    /// <summary>Snapshot of the step's stable name key.</summary>
    public string StepName { get; private set; } = default!;

    /// <summary>Snapshot of the step kind at execution time.</summary>
    public StepKind Kind { get; private set; }

    /// <summary>Snapshot of the sort order.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Snapshot of the RunIfExpression (null if none).</summary>
    public string? RunIfExpressionSnapshot { get; private set; }

    /// <summary>Snapshot of the ParametersJson (null if none).</summary>
    public string? ParametersJsonSnapshot { get; private set; }

    public StepOutcome Outcome { get; private set; }

    /// <summary>Only set when Outcome = Skipped.</summary>
    public SkipReason? SkipReason { get; private set; }

    public int DurationMs { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTime CreatedOn { get; private set; }

    private ActivityProcessExecution() { }

    public static ActivityProcessExecution Record(
        Guid workflowInstanceId,
        Guid workflowActivityExecutionId,
        Guid? configurationId,
        int configurationVersion,
        string stepName,
        StepKind kind,
        int sortOrder,
        string? runIfExpressionSnapshot,
        string? parametersJsonSnapshot,
        StepOutcome outcome,
        Entities.SkipReason? skipReason,
        int durationMs,
        string? errorMessage)
    {
        return new ActivityProcessExecution
        {
            Id = Guid.CreateVersion7(),
            WorkflowInstanceId = workflowInstanceId,
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            ConfigurationId = configurationId,
            ConfigurationVersion = configurationVersion,
            StepName = stepName,
            Kind = kind,
            SortOrder = sortOrder,
            RunIfExpressionSnapshot = runIfExpressionSnapshot,
            ParametersJsonSnapshot = parametersJsonSnapshot,
            Outcome = outcome,
            SkipReason = skipReason,
            DurationMs = durationMs,
            ErrorMessage = errorMessage,
            CreatedOn = DateTime.Now
        };
    }
}
