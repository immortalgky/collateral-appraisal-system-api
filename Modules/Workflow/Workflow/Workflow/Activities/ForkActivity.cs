using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities;

public class ForkActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.ForkActivity;
    public override string Name => "Fork Activity";
    public override string Description => "Splits workflow execution into multiple parallel branches";

    protected override async Task<ActivityResult> OnExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var branches = GetProperty<List<ForkBranch>>(context, "branches", new List<ForkBranch>());
        var forkType = GetProperty<string>(context, "forkType", "all");
        var maxConcurrency = GetProperty<int>(context, "maxConcurrency", int.MaxValue);

        if (!branches.Any())
        {
            return ActivityResult.Failed("Fork activity must have at least one branch");
        }

        // Validate branches
        foreach (var branch in branches)
        {
            if (string.IsNullOrEmpty(branch.Id))
            {
                return ActivityResult.Failed("All fork branches must have an ID");
            }
        }

        // Create fork execution context
        var forkContext = new ForkExecutionContext
        {
            ForkId = Guid.NewGuid().ToString(),
            ActivityId = context.ActivityId,
            WorkflowInstanceId = context.WorkflowInstanceId,
            Branches = branches,
            ForkType = forkType,
            MaxConcurrency = maxConcurrency,
            CreatedAt = DateTime.UtcNow,
            Status = ForkStatus.Active
        };

        // Filter branches based on conditions
        var activeBranches = await FilterBranchesAsync(branches, context, cancellationToken);

        if (!activeBranches.Any())
        {
            return ActivityResult.Success(new Dictionary<string, object>
            {
                ["forkId"] = forkContext.ForkId,
                ["activeBranches"] = 0,
                ["message"] = "No branches met the activation criteria"
            });
        }

        // Store fork context in variables for the join activity to use
        var outputData = new Dictionary<string, object>
        {
            ["forkId"] = forkContext.ForkId,
            ["activeBranches"] = activeBranches.Count,
            ["branchIds"] = activeBranches.Select(b => b.Id).ToList(),
            ["forkType"] = forkType,
            ["maxConcurrency"] = maxConcurrency
        };

        // Add fork tracking variables to output data
        outputData[$"fork_{forkContext.ForkId}"] = forkContext;
        outputData[$"fork_{forkContext.ForkId}_branches"] = activeBranches;

        return ActivityResult.Success(outputData);
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var branches = GetProperty<List<ForkBranch>>(context, "branches", new List<ForkBranch>());

        if (!branches.Any())
        {
            errors.Add("Fork activity must have at least one branch");
        }
        else
        {
            var branchIds = new HashSet<string>();
            foreach (var branch in branches)
            {
                if (string.IsNullOrWhiteSpace(branch.Id))
                {
                    errors.Add("All branches must have a unique ID");
                }
                else if (!branchIds.Add(branch.Id))
                {
                    errors.Add($"Duplicate branch ID: {branch.Id}");
                }

                if (string.IsNullOrWhiteSpace(branch.Name))
                {
                    errors.Add($"Branch {branch.Id} must have a name");
                }
            }
        }

        var forkType = GetProperty<string>(context, "forkType", "all");
        if (!new[] { "all", "any", "conditional" }.Contains(forkType))
        {
            errors.Add("Fork type must be 'all', 'any', or 'conditional'");
        }

        var maxConcurrency = GetProperty<int>(context, "maxConcurrency", int.MaxValue);
        if (maxConcurrency <= 0)
        {
            errors.Add("Max concurrency must be greater than 0");
        }

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    private async Task<List<ForkBranch>> FilterBranchesAsync(List<ForkBranch> branches, ActivityContext context, CancellationToken cancellationToken)
    {
        var activeBranches = new List<ForkBranch>();

        foreach (var branch in branches)
        {
            if (string.IsNullOrWhiteSpace(branch.Condition))
            {
                // No condition means always active
                activeBranches.Add(branch);
            }
            else
            {
                // Evaluate branch condition using the new expression engine
                var shouldActivate = EvaluateCondition(context, branch.Condition);
                if (shouldActivate)
                {
                    activeBranches.Add(branch);
                }
            }
        }

        return activeBranches;
    }
}

public class ForkBranch
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? Condition { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, object> OutputData { get; set; } = new();
    public int Priority { get; set; } = 0;
}

public class ForkExecutionContext
{
    public string ForkId { get; set; } = default!;
    public string ActivityId { get; set; } = default!;
    public Guid WorkflowInstanceId { get; set; }
    public List<ForkBranch> Branches { get; set; } = new();
    public string ForkType { get; set; } = default!;
    public int MaxConcurrency { get; set; }
    public DateTime CreatedAt { get; set; }
    public ForkStatus Status { get; set; }
    public Dictionary<string, BranchExecutionResult> BranchResults { get; set; } = new();
}

public class BranchExecutionResult
{
    public string BranchId { get; set; } = default!;
    public BranchStatus Status { get; set; }
    public Dictionary<string, object> OutputData { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum ForkStatus
{
    Active,
    Completed,
    Failed,
    Cancelled
}

public enum BranchStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}