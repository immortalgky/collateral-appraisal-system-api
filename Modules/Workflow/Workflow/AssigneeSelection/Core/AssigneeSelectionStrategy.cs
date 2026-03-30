namespace Workflow.AssigneeSelection.Core;

public enum AssigneeSelectionStrategy
{
    /// <summary>
    /// Manual assignment by administrator
    /// </summary>
    Manual,

    /// <summary>
    /// Round-robin assignment to distribute load evenly
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Workload-based assignment to balance current workload
    /// </summary>
    WorkloadBased,

    /// <summary>
    /// Random assignment
    /// </summary>
    Random,

    /// <summary>
    /// Assign to the user who completed this activity previously (route-back)
    /// </summary>
    PreviousOwner,

    /// <summary>
    /// Assign to supervisor/manager
    /// </summary>
    Supervisor,

    /// <summary>
    /// Team-constrained assignment using the pipeline's pre-filtered candidate pool
    /// </summary>
    TeamConstrained,

    /// <summary>
    /// Assign to the user who originally started the workflow instance
    /// </summary>
    StartedBy,

    /// <summary>
    /// Assign to a pool/group — all members can see and claim the task
    /// </summary>
    Pool,

    /// <summary>
    /// Assign to a user specified in a named workflow variable
    /// </summary>
    VariableAssignee
}

/// <summary>
/// Extension methods for AssignmentStrategy enum
/// </summary>
public static class AssignmentStrategyExtensions
{
    /// <summary>
    /// Converts the enum to its string representation for configuration
    /// </summary>
    public static string ToStringValue(this AssigneeSelectionStrategy strategy)
    {
        return strategy switch
        {
            AssigneeSelectionStrategy.Manual => "manual",
            AssigneeSelectionStrategy.RoundRobin => "round_robin",
            AssigneeSelectionStrategy.WorkloadBased => "workload_based",
            AssigneeSelectionStrategy.Random => "random",
            AssigneeSelectionStrategy.PreviousOwner => "previous_owner",
            AssigneeSelectionStrategy.Supervisor => "supervisor",
            AssigneeSelectionStrategy.TeamConstrained => "team_constrained",
            AssigneeSelectionStrategy.StartedBy => "started_by",
            AssigneeSelectionStrategy.Pool => "pool",
            AssigneeSelectionStrategy.VariableAssignee => "variable_assignee",
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown assignment strategy")
        };
    }

    /// <summary>
    /// Parses a string to AssigneeSelectionStrategy enum
    /// </summary>
    public static AssigneeSelectionStrategy FromString(string strategyString)
    {
        return strategyString?.ToLower() switch
        {
            "manual" => AssigneeSelectionStrategy.Manual,
            "round_robin" => AssigneeSelectionStrategy.RoundRobin,
            "workload_based" => AssigneeSelectionStrategy.WorkloadBased,
            "random" => AssigneeSelectionStrategy.Random,
            "previous_owner" => AssigneeSelectionStrategy.PreviousOwner,
            "supervisor" => AssigneeSelectionStrategy.Supervisor,
            "team_constrained" => AssigneeSelectionStrategy.TeamConstrained,
            "started_by" => AssigneeSelectionStrategy.StartedBy,
            "pool" => AssigneeSelectionStrategy.Pool,
            "variable_assignee" => AssigneeSelectionStrategy.VariableAssignee,
            _ => throw new ArgumentException($"Unknown assignment strategy: {strategyString}", nameof(strategyString))
        };
    }
}