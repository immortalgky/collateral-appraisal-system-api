using Shared.DDD;

namespace Workflow.Data.Entities;

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
    /// Execution order within the activity
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Optional JSON config per step (e.g., {"targetStatus": "UnderReview"})
    /// </summary>
    public string? Parameters { get; private set; }

    /// <summary>
    /// Soft toggle to enable/disable this step
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public new string CreatedBy { get; private set; } = default!;
    public DateTime UpdatedAt { get; private set; }
    public new string UpdatedBy { get; private set; } = default!;

    private ActivityProcessConfiguration()
    {
        // For EF Core
    }

    public static ActivityProcessConfiguration Create(
        string activityName,
        string stepName,
        string processorName,
        int sortOrder,
        string createdBy,
        string? parameters = null)
    {
        return new ActivityProcessConfiguration
        {
            Id = Guid.CreateVersion7(),
            ActivityName = activityName,
            StepName = stepName,
            ProcessorName = processorName,
            SortOrder = sortOrder,
            Parameters = parameters,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };
    }
}
