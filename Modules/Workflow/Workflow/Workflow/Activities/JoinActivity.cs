using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities;

public class JoinActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.JoinActivity;
    public override string Name => "Join Activity";
    public override string Description => "Synchronizes and merges multiple parallel workflow branches";

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var forkId = GetProperty<string>(context, "forkId");
        var joinType = GetProperty<string>(context, "joinType", "all");
        var timeoutMinutes = GetProperty<int>(context, "timeoutMinutes", 0);
        var mergeStrategy = GetProperty<string>(context, "mergeStrategy", "combine");

        if (string.IsNullOrWhiteSpace(forkId))
        {
            return ActivityResult.Failed("Join activity requires a forkId to identify which fork to join");
        }

        // Get fork context from workflow variables
        var forkContextKey = $"fork_{forkId}";
        var forkContext = GetVariable<ForkExecutionContext>(context, forkContextKey);
        
        if (forkContext == null)
        {
            return ActivityResult.Failed($"Fork context not found for forkId: {forkId}");
        }

        // Get branch execution results
        var branchResultsKey = $"fork_{forkId}_results";
        var branchResults = GetVariable<Dictionary<string, BranchExecutionResult>>(context, branchResultsKey, new Dictionary<string, BranchExecutionResult>());

        // Evaluate join condition
        var joinResult = await EvaluateJoinConditionAsync(forkContext, branchResults, joinType, timeoutMinutes, cancellationToken);

        if (!joinResult.CanProceed)
        {
            if (joinResult.IsTimeout)
            {
                return await HandleJoinTimeoutAsync(forkContext, branchResults, context, cancellationToken);
            }

            // Not ready to join yet, keep activity pending
            return ActivityResult.Pending();
        }

        // Merge branch data based on strategy
        var mergedData = await MergeBranchDataAsync(branchResults, mergeStrategy, cancellationToken);

        // Create output data
        var outputData = new Dictionary<string, object>
        {
            ["forkId"] = forkId,
            ["joinType"] = joinType,
            ["completedBranches"] = branchResults.Count(br => br.Value.Status == BranchStatus.Completed),
            ["totalBranches"] = forkContext.Branches.Count,
            ["mergedData"] = mergedData,
            ["joinCompletedAt"] = DateTime.UtcNow
        };

        // Add cleanup variables to output data
        outputData[forkContextKey] = null!; // Clean up fork context
        outputData[branchResultsKey] = null!; // Clean up branch results
        outputData["lastJoinResult"] = outputData;

        return ActivityResult.Success(outputData);
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var forkId = GetProperty<string>(context, "forkId");
        if (string.IsNullOrWhiteSpace(forkId))
        {
            errors.Add("Join activity requires a forkId property");
        }

        var joinType = GetProperty<string>(context, "joinType", "all");
        if (!new[] { "all", "any", "first", "majority" }.Contains(joinType))
        {
            errors.Add("Join type must be 'all', 'any', 'first', or 'majority'");
        }

        var mergeStrategy = GetProperty<string>(context, "mergeStrategy", "combine");
        if (!new[] { "combine", "override", "first", "last" }.Contains(mergeStrategy))
        {
            errors.Add("Merge strategy must be 'combine', 'override', 'first', or 'last'");
        }

        var timeoutMinutes = GetProperty<int>(context, "timeoutMinutes", 0);
        if (timeoutMinutes < 0)
        {
            errors.Add("Timeout minutes cannot be negative");
        }

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    private async Task<JoinEvaluationResult> EvaluateJoinConditionAsync(
        ForkExecutionContext forkContext,
        Dictionary<string, BranchExecutionResult> branchResults,
        string joinType,
        int timeoutMinutes,
        CancellationToken cancellationToken)
    {
        var activeBranchIds = forkContext.Branches.Select(b => b.Id).ToHashSet();
        var completedBranches = branchResults.Where(br => activeBranchIds.Contains(br.Key) && br.Value.Status == BranchStatus.Completed).ToList();
        var failedBranches = branchResults.Where(br => activeBranchIds.Contains(br.Key) && br.Value.Status == BranchStatus.Failed).ToList();
        var totalActiveBranches = activeBranchIds.Count;

        // Check timeout
        var isTimeout = timeoutMinutes > 0 && 
                       DateTime.UtcNow > forkContext.CreatedAt.AddMinutes(timeoutMinutes);

        var result = joinType switch
        {
            "all" => new JoinEvaluationResult
            {
                CanProceed = completedBranches.Count == totalActiveBranches || 
                           (completedBranches.Count + failedBranches.Count == totalActiveBranches),
                IsTimeout = isTimeout,
                CompletedBranches = completedBranches.Count,
                TotalBranches = totalActiveBranches
            },
            "any" => new JoinEvaluationResult
            {
                CanProceed = completedBranches.Any(),
                IsTimeout = isTimeout && !completedBranches.Any(),
                CompletedBranches = completedBranches.Count,
                TotalBranches = totalActiveBranches
            },
            "first" => new JoinEvaluationResult
            {
                CanProceed = completedBranches.Any(),
                IsTimeout = isTimeout && !completedBranches.Any(),
                CompletedBranches = completedBranches.Count,
                TotalBranches = totalActiveBranches
            },
            "majority" => new JoinEvaluationResult
            {
                CanProceed = completedBranches.Count > totalActiveBranches / 2.0,
                IsTimeout = isTimeout,
                CompletedBranches = completedBranches.Count,
                TotalBranches = totalActiveBranches
            },
            _ => new JoinEvaluationResult
            {
                CanProceed = false,
                IsTimeout = isTimeout,
                CompletedBranches = completedBranches.Count,
                TotalBranches = totalActiveBranches
            }
        };

        return await Task.FromResult(result);
    }

    private async Task<Dictionary<string, object>> MergeBranchDataAsync(
        Dictionary<string, BranchExecutionResult> branchResults,
        string mergeStrategy,
        CancellationToken cancellationToken)
    {
        var mergedData = new Dictionary<string, object>();
        var completedResults = branchResults.Where(br => br.Value.Status == BranchStatus.Completed).ToList();

        switch (mergeStrategy)
        {
            case "combine":
                // Combine all outputs, with later branches overriding earlier ones for conflicting keys
                foreach (var result in completedResults.OrderBy(r => r.Value.CompletedAt))
                {
                    foreach (var kvp in result.Value.OutputData)
                    {
                        mergedData[kvp.Key] = kvp.Value;
                    }
                }
                break;

            case "override":
                // Last completed branch wins
                var lastResult = completedResults.OrderByDescending(r => r.Value.CompletedAt).FirstOrDefault();
                if (lastResult.Value != null)
                {
                    foreach (var kvp in lastResult.Value.OutputData)
                    {
                        mergedData[kvp.Key] = kvp.Value;
                    }
                }
                break;

            case "first":
                // First completed branch wins
                var firstResult = completedResults.OrderBy(r => r.Value.CompletedAt).FirstOrDefault();
                if (firstResult.Value != null)
                {
                    foreach (var kvp in firstResult.Value.OutputData)
                    {
                        mergedData[kvp.Key] = kvp.Value;
                    }
                }
                break;

            case "last":
                // Same as override - last completed branch wins
                var latestResult = completedResults.OrderByDescending(r => r.Value.CompletedAt).FirstOrDefault();
                if (latestResult.Value != null)
                {
                    foreach (var kvp in latestResult.Value.OutputData)
                    {
                        mergedData[kvp.Key] = kvp.Value;
                    }
                }
                break;
        }

        // Add branch-specific results for reference
        mergedData["branchResults"] = completedResults.ToDictionary(
            r => r.Key,
            r => new
            {
                r.Value.Status,
                r.Value.OutputData,
                r.Value.CompletedAt,
                r.Value.StartedAt
            });

        return await Task.FromResult(mergedData);
    }

    private async Task<ActivityResult> HandleJoinTimeoutAsync(
        ForkExecutionContext forkContext,
        Dictionary<string, BranchExecutionResult> branchResults,
        ActivityContext context,
        CancellationToken cancellationToken)
    {
        var timeoutAction = GetProperty<string>(context, "timeoutAction", "fail");

        var outputData = new Dictionary<string, object>
        {
            ["forkId"] = forkContext.ForkId,
            ["timeout"] = true,
            ["completedBranches"] = branchResults.Count(br => br.Value.Status == BranchStatus.Completed),
            ["totalBranches"] = forkContext.Branches.Count,
            ["timeoutAction"] = timeoutAction,
            ["timeoutAt"] = DateTime.UtcNow
        };

        return timeoutAction switch
        {
            "proceed" => ActivityResult.Success(outputData),
            "fail" => ActivityResult.Failed($"Join activity timed out. Only {branchResults.Count(br => br.Value.Status == BranchStatus.Completed)} of {forkContext.Branches.Count} branches completed."),
            _ => ActivityResult.Failed("Join activity timed out with unknown timeout action")
        };
    }
}

public class JoinEvaluationResult
{
    public bool CanProceed { get; set; }
    public bool IsTimeout { get; set; }
    public int CompletedBranches { get; set; }
    public int TotalBranches { get; set; }
}