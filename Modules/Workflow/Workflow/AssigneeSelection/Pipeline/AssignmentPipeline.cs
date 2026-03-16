using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Engine;
using Workflow.Workflow.Activities.Core;

namespace Workflow.AssigneeSelection.Pipeline;

public class AssignmentPipeline : IAssignmentPipeline
{
    private readonly IAssignmentContextBuilder _contextBuilder;
    private readonly IEnumerable<IAssignmentFilter> _filters;
    private readonly ICascadingAssignmentEngine _engine;
    private readonly IEnumerable<IAssignmentValidator> _validators;
    private readonly IAssignmentFinalizer _finalizer;
    private readonly ILogger<AssignmentPipeline> _logger;

    private const int MaxRetries = 3;

    public AssignmentPipeline(
        IAssignmentContextBuilder contextBuilder,
        IEnumerable<IAssignmentFilter> filters,
        ICascadingAssignmentEngine engine,
        IEnumerable<IAssignmentValidator> validators,
        IAssignmentFinalizer finalizer,
        ILogger<AssignmentPipeline> logger)
    {
        _contextBuilder = contextBuilder;
        _filters = filters;
        _engine = engine;
        _validators = validators;
        _finalizer = finalizer;
        _logger = logger;
    }

    public async Task<AssignmentResult> AssignAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var pipelineCtx = new AssignmentPipelineContext { ActivityContext = context };

        // Stage 1: Build context
        await _contextBuilder.BuildAsync(pipelineCtx, cancellationToken);

        _logger.LogInformation(
            "Pipeline Stage 1 complete for {ActivityId}. TeamId={TeamId}, Rules={Rules}, PriorAssignees={Count}",
            context.ActivityId, pipelineCtx.TeamId, pipelineCtx.Rules, pipelineCtx.PriorAssignees.Count);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            // Stage 2: Filter candidates
            pipelineCtx.CandidatePool = await ApplyFiltersAsync(pipelineCtx, cancellationToken);

            _logger.LogInformation(
                "Pipeline Stage 2 complete for {ActivityId}. CandidatePool={Count} (attempt {Attempt})",
                context.ActivityId, pipelineCtx.CandidatePool.Count, attempt + 1);

            if (pipelineCtx.CandidatePool.Count == 0 && pipelineCtx.Rules.TeamConstrained)
            {
                _logger.LogWarning("No candidates after filtering for {ActivityId}", context.ActivityId);
                return new AssignmentResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No eligible candidates found after team/exclusion filtering"
                };
            }

            // Stage 3: Select assignee
            var selectionResult = await SelectAssigneeAsync(pipelineCtx, cancellationToken);
            if (!selectionResult.IsSuccess)
                return selectionResult;

            pipelineCtx.SelectedAssignee = selectionResult.AssigneeId;
            pipelineCtx.SelectionStrategy = selectionResult.Strategy;
            pipelineCtx.SelectionMetadata = selectionResult.Metadata;

            _logger.LogInformation(
                "Pipeline Stage 3 complete for {ActivityId}. Selected={Assignee} via {Strategy}",
                context.ActivityId, pipelineCtx.SelectedAssignee, pipelineCtx.SelectionStrategy);

            // Stage 4: Validate
            pipelineCtx.ValidationPassed = true;
            pipelineCtx.ValidationErrors.Clear();

            foreach (var validator in _validators)
            {
                var validationResult = await validator.ValidateAsync(pipelineCtx, cancellationToken);
                if (!validationResult.IsValid)
                {
                    pipelineCtx.ValidationPassed = false;
                    pipelineCtx.ValidationErrors.AddRange(validationResult.Errors);
                }
            }

            if (pipelineCtx.ValidationPassed)
            {
                _logger.LogInformation("Pipeline Stage 4 passed for {ActivityId}", context.ActivityId);
                break;
            }

            _logger.LogWarning(
                "Pipeline Stage 4 failed for {ActivityId} (attempt {Attempt}): {Errors}",
                context.ActivityId, attempt + 1, string.Join("; ", pipelineCtx.ValidationErrors));

            if (attempt == MaxRetries - 1)
            {
                return new AssignmentResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Assignment validation failed after {MaxRetries} attempts: {string.Join("; ", pipelineCtx.ValidationErrors)}"
                };
            }
        }

        // Stage 5: Finalize
        var finalResult = await _finalizer.FinalizeAsync(pipelineCtx, cancellationToken);

        _logger.LogInformation(
            "Pipeline complete for {ActivityId}. Assignee={Assignee}",
            context.ActivityId, finalResult.AssigneeId);

        return finalResult;
    }

    public async Task<AssignmentPipelineContext> GetEligibleAssigneesAsync(
        ActivityContext context, CancellationToken cancellationToken = default)
    {
        var pipelineCtx = new AssignmentPipelineContext { ActivityContext = context };

        // Stage 1 + Stage 2 only
        await _contextBuilder.BuildAsync(pipelineCtx, cancellationToken);
        pipelineCtx.CandidatePool = await ApplyFiltersAsync(pipelineCtx, cancellationToken);

        return pipelineCtx;
    }

    private async Task<List<Teams.TeamMemberInfo>> ApplyFiltersAsync(
        AssignmentPipelineContext pipelineCtx, CancellationToken cancellationToken)
    {
        var candidates = pipelineCtx.CandidatePool;

        foreach (var filter in _filters)
        {
            candidates = await filter.FilterAsync(pipelineCtx, candidates, cancellationToken);
        }

        return candidates;
    }

    private async Task<AssignmentResult> SelectAssigneeAsync(
        AssignmentPipelineContext pipelineCtx, CancellationToken cancellationToken)
    {
        var activityCtx = pipelineCtx.ActivityContext;

        // Manual pick via RuntimeOverride takes priority
        if (pipelineCtx.RuntimeOverride?.RuntimeAssignee is { } manualAssignee
            && !string.IsNullOrEmpty(manualAssignee))
        {
            return new AssignmentResult
            {
                IsSuccess = true,
                AssigneeId = manualAssignee,
                Strategy = "ManualPick",
                Metadata = new Dictionary<string, object>
                {
                    ["source"] = "RuntimeOverride",
                    ["overrideBy"] = pipelineCtx.RuntimeOverride.OverrideBy ?? "Unknown",
                    ["reason"] = pipelineCtx.RuntimeOverride.OverrideReason ?? ""
                }
            };
        }

        // Build AssignmentContext for the cascading engine
        var strategies = pipelineCtx.RuntimeOverride?.RuntimeAssignmentStrategies
            ?? GetStrategiesFromProperties(activityCtx);

        var userGroups = GetUserGroupsFromProperties(activityCtx);

        var assignmentContext = new AssignmentContext
        {
            ActivityName = activityCtx.ActivityId,
            AssignmentStrategies = strategies,
            UserGroups = userGroups,
            UserCode = pipelineCtx.RuntimeOverride?.RuntimeAssignee ?? GetPropertyString(activityCtx, "assignee"),
            DueDate = DateTime.UtcNow.AddDays(7),
            Properties = activityCtx.Properties,
            StartedBy = activityCtx.WorkflowInstance.StartedBy,
            CandidatePool = pipelineCtx.CandidatePool
        };

        var engineResult = await _engine.ExecuteAsync(assignmentContext, cancellationToken);

        return new AssignmentResult
        {
            IsSuccess = engineResult.IsSuccess,
            AssigneeId = engineResult.AssigneeId,
            Strategy = engineResult.Metadata?.TryGetValue("SuccessfulStrategy", out var s) == true ? s?.ToString() : "CascadingEngine",
            Metadata = engineResult.Metadata,
            ErrorMessage = engineResult.ErrorMessage
        };
    }

    private static List<string> GetStrategiesFromProperties(ActivityContext ctx)
    {
        if (ctx.Properties.TryGetValue("assignmentStrategies", out var val) && val is List<string> list)
            return list;

        if (ctx.Properties.TryGetValue("assignmentStrategy", out var single) && single is string s && !string.IsNullOrEmpty(s))
            return [s];

        return ["round_robin", "workload_based"];
    }

    private static List<string> GetUserGroupsFromProperties(ActivityContext ctx)
    {
        if (ctx.Properties.TryGetValue("assigneeGroup", out var val))
        {
            if (val is List<string> list) return list;
            if (val is string s && !string.IsNullOrEmpty(s)) return [s];
        }

        return [];
    }

    private static string GetPropertyString(ActivityContext ctx, string key)
    {
        return ctx.Properties.TryGetValue(key, out var val) && val is string s ? s : "";
    }
}
