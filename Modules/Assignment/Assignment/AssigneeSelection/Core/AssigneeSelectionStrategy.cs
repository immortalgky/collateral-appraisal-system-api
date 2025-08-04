namespace Assignment.AssigneeSelection.Core;

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
    Random
}

/// <summary>
/// Extension methods for AssignmentStrategy enum
/// </summary>
public static class AssignmentStrategyExtensions
{
    /// <summary>
    /// Converts the enum to its string representation for configuration
    /// </summary>
    public static string ToStringValue(this AssigneeSelectionStrategy strategy) => strategy switch
    {
        AssigneeSelectionStrategy.Manual => "manual",
        AssigneeSelectionStrategy.RoundRobin => "round_robin",
        AssigneeSelectionStrategy.WorkloadBased => "workload_based",
        AssigneeSelectionStrategy.Random => "random",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown assignment strategy")
    };

    /// <summary>
    /// Parses a string to AssigneeSelectionStrategy enum
    /// </summary>
    public static AssigneeSelectionStrategy FromString(string strategyString) => strategyString?.ToLower() switch
    {
        "manual" => AssigneeSelectionStrategy.Manual,
        "round_robin" => AssigneeSelectionStrategy.RoundRobin,
        "workload_based" => AssigneeSelectionStrategy.WorkloadBased,
        "random" => AssigneeSelectionStrategy.Random,
        _ => throw new ArgumentException($"Unknown assignment strategy: {strategyString}", nameof(strategyString))
    };
}