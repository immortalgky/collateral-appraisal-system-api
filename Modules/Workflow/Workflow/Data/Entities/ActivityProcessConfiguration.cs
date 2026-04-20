using Shared.DDD;

namespace Workflow.Data.Entities;

/// <summary>
/// Discriminator for whether a pipeline step validates preconditions or performs a side effect.
/// </summary>
public enum StepKind : byte
{
    /// <summary>Validates a precondition; failures are collected and surfaced together.</summary>
    Validation = 0,
    /// <summary>Performs a side effect; first failure halts the pipeline.</summary>
    Action = 1
}

/// <summary>
/// Configuration for process steps that run when an activity is completed.
/// Each row = one step in the pipeline, ordered by SortOrder.
/// </summary>
public class ActivityProcessConfiguration : Entity<Guid>
{
    /// <summary>
    /// Workflow activity name this step applies to (e.g., "site-inspection")
    /// </summary>
    public string ActivityName { get; private set; } = default!;

    /// <summary>
    /// Human-readable label (e.g., "Validate appraised value")
    /// </summary>
    public string StepName { get; private set; } = default!;

    /// <summary>
    /// C# class name to resolve from DI (e.g., "UpdateAppraisalStatus")
    /// </summary>
    public string ProcessorName { get; private set; } = default!;

    /// <summary>
    /// Whether this step is a Validation (collect-all) or Action (stop-on-first).
    /// </summary>
    public StepKind Kind { get; private set; }

    /// <summary>
    /// Execution order within the activity (sorted within Kind bucket).
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Optional Jint expression evaluated before the step runs.
    /// Null or empty = always run. Non-boolean or runtime error halts the pipeline.
    /// </summary>
    public string? RunIfExpression { get; private set; }

    /// <summary>
    /// Optional JSON config per step (e.g., {"targetStatus": "UnderReview"})
    /// </summary>
    public string? ParametersJson { get; private set; }

    /// <summary>
    /// Soft toggle to enable/disable this step.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Incremented on every update, activate, or deactivate.
    /// Used to invalidate the Jint compile cache.
    /// </summary>
    public int Version { get; private set; }

    public new DateTime CreatedAt { get; private set; }
    public new string CreatedBy { get; private set; } = default!;
    public new DateTime UpdatedAt { get; private set; }
    public new string UpdatedBy { get; private set; } = default!;

    private ActivityProcessConfiguration()
    {
        // For EF Core
    }

    public static ActivityProcessConfiguration Create(
        string activityName,
        string stepName,
        string processorName,
        StepKind kind,
        int sortOrder,
        string createdBy,
        string? parametersJson = null,
        string? runIfExpression = null)
    {
        return new ActivityProcessConfiguration
        {
            Id = Guid.CreateVersion7(),
            ActivityName = activityName,
            StepName = stepName,
            ProcessorName = processorName,
            Kind = kind,
            SortOrder = sortOrder,
            ParametersJson = parametersJson,
            RunIfExpression = runIfExpression,
            IsActive = true,
            Version = 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };
    }

    /// <summary>Legacy create overload for backward compatibility with existing seeders.</summary>
    public static ActivityProcessConfiguration Create(
        string activityName,
        string stepName,
        string processorName,
        int sortOrder,
        string createdBy,
        string? parameters = null)
        => Create(activityName, stepName, processorName, StepKind.Validation, sortOrder, createdBy, parameters);

    public void Update(
        StepKind kind,
        int sortOrder,
        string? parametersJson,
        string? runIfExpression,
        bool isActive,
        string updatedBy)
    {
        Kind = kind;
        SortOrder = sortOrder;
        ParametersJson = parametersJson;
        RunIfExpression = runIfExpression;
        IsActive = isActive;
        Version++;
        UpdatedAt = DateTime.Now;
        UpdatedBy = updatedBy;
    }

    public void SetActive(bool isActive, string updatedBy)
    {
        IsActive = isActive;
        Version++;
        UpdatedAt = DateTime.Now;
        UpdatedBy = updatedBy;
    }
}
