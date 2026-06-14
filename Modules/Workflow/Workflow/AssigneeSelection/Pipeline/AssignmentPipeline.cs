using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Engine;
using Workflow.Services.Configuration.Models;
using Workflow.Workflow;
using Workflow.Workflow.Activities.Core;

namespace Workflow.AssigneeSelection.Pipeline;

public class AssignmentPipeline : IAssignmentPipeline
{
    private readonly IAssignmentContextBuilder _contextBuilder;
    private readonly IEnumerable<IAssignmentFilter> _filters;
    private readonly ICascadingAssignmentEngine _engine;
    private readonly IEnumerable<IAssignmentValidator> _validators;
    private readonly IAssignmentFinalizer _finalizer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AssignmentPipeline> _logger;

    private const int MaxRetries = 3;

    public AssignmentPipeline(
        IAssignmentContextBuilder contextBuilder,
        IEnumerable<IAssignmentFilter> filters,
        ICascadingAssignmentEngine engine,
        IEnumerable<IAssignmentValidator> validators,
        IAssignmentFinalizer finalizer,
        IDateTimeProvider dateTimeProvider,
        ILogger<AssignmentPipeline> logger)
    {
        _contextBuilder = contextBuilder;
        _filters = filters;
        _engine = engine;
        _validators = validators;
        _finalizer = finalizer;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<AssignmentResult> AssignAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var pipelineCtx = new AssignmentPipelineContext { ActivityContext = context };

        // Stage 1: Build context (also resolves the DB override onto pipelineCtx.ExternalConfig)
        await _contextBuilder.BuildAsync(pipelineCtx, cancellationToken);

        _logger.LogInformation(
            "Pipeline Stage 1 complete for {ActivityId}. TeamId={TeamId}, Rules={Rules}, PriorAssignees={Count}",
            context.ActivityId, pipelineCtx.TeamId, pipelineCtx.Rules, pipelineCtx.PriorAssignees.Count);

        var result = await RunStagesAsync(pipelineCtx, cancellationToken);

        // Admin-pool fallback: when assignment fails and the DB override opts in. The config was already
        // resolved in Stage 1, so no extra DB round-trip is needed (this replaces TaskActivity's own load).
        if (!result.IsSuccess && pipelineCtx.ExternalConfig?.EscalateToAdminPool == true)
        {
            var fallback = TryAdminPoolFallback(pipelineCtx.ExternalConfig);
            if (fallback != null)
            {
                _logger.LogWarning(
                    "Pipeline assignment failed for {ActivityId}; admin-pool fallback applied", context.ActivityId);
                return fallback;
            }
        }

        return result;
    }

    private async Task<AssignmentResult> RunStagesAsync(
        AssignmentPipelineContext pipelineCtx, CancellationToken cancellationToken)
    {
        var context = pipelineCtx.ActivityContext;

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

    // Admin-pool fallback assignee when all strategies fail and the DB override has EscalateToAdminPool.
    private AssignmentResult? TryAdminPoolFallback(TaskAssignmentConfigurationDto config)
    {
        try
        {
            var adminPoolId = !string.IsNullOrEmpty(config.AdminPoolId) ? config.AdminPoolId : "ADMIN_POOL";

            _logger.LogInformation("Admin pool fallback - assigning to: {AdminPoolId}", adminPoolId);

            return new AssignmentResult
            {
                IsSuccess = true,
                AssigneeId = adminPoolId,
                Strategy = "AdminPoolFallback",
                Metadata = new Dictionary<string, object>
                {
                    ["AdminPoolId"] = adminPoolId,
                    ["IsFallbackAssignment"] = true,
                    ["FallbackReason"] = "Pipeline assignment strategies failed"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin pool fallback failed");
            return null;
        }
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

        // Detect revisit (route-back) to choose correct strategy list
        var isRevisit = await _engine.IsRouteBackScenarioAsync(
            activityCtx.WorkflowInstance.Id, activityCtx.ActivityId, cancellationToken);

        // Strategy precedence: RuntimeOverride > DB config (Primary/RouteBack) > JSON definition.
        // An empty DB strategy list falls through; a DB SpecificAssignee with no strategies implies Manual.
        var dbStrategies = isRevisit
            ? pipelineCtx.ExternalConfig?.RouteBackStrategies
            : pipelineCtx.ExternalConfig?.PrimaryStrategies;

        var strategies = pipelineCtx.RuntimeOverride?.RuntimeAssignmentStrategies
            ?? (dbStrategies is { Count: > 0 } ? dbStrategies
                : !string.IsNullOrEmpty(pipelineCtx.ExternalConfig?.SpecificAssignee) ? ["Manual"] : null)
            ?? GetStrategiesForScenario(activityCtx, isRevisit);

        _logger.LogInformation(
            "Strategy selection for {ActivityId}: IsRevisit={IsRevisit}, Strategies=[{Strategies}]",
            activityCtx.ActivityId, isRevisit, string.Join(",", strategies));

        var userGroups = ResolveUserGroups(pipelineCtx);

        var assignmentContext = new AssignmentContext
        {
            WorkflowInstanceId = activityCtx.WorkflowInstance.Id,
            ActivityName = activityCtx.ActivityId,
            AssignmentStrategies = strategies,
            UserGroups = userGroups,
            UserCode = pipelineCtx.RuntimeOverride?.RuntimeAssignee
                       ?? JsonPropertyReader.NullIfEmpty(pipelineCtx.ExternalConfig?.SpecificAssignee)
                       ?? GetPropertyString(activityCtx.Properties, "assignee") ?? "",
            DueDate = _dateTimeProvider.ApplicationNow.AddDays(7),
            Properties = activityCtx.Properties,
            StartedBy = activityCtx.WorkflowInstance.StartedBy,
            CandidatePool = pipelineCtx.CandidatePool,
            Variables = activityCtx.Variables
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

    private static List<string> GetStrategiesForScenario(ActivityContext ctx, bool isRevisit)
    {
        var key = isRevisit ? "revisitAssignmentStrategies" : "initialAssignmentStrategies";
        var strategies = GetPropertyStringList(ctx.Properties, key);
        if (strategies.Count > 0) return strategies;

        return GetStrategiesFromProperties(ctx);
    }

    private static List<string> GetStrategiesFromProperties(ActivityContext ctx)
    {
        var strategies = GetPropertyStringList(ctx.Properties, "assignmentStrategies");
        if (strategies.Count > 0) return strategies;

        var single = GetPropertyString(ctx.Properties, "assignmentStrategies");
        if (!string.IsNullOrEmpty(single)) return [single];

        single = GetPropertyString(ctx.Properties, "assignmentStrategy");
        if (!string.IsNullOrEmpty(single)) return [single];

        return ["round_robin", "workload_based"];
    }

    // Reads the group resolved once in AssignmentContextBuilder (RuntimeOverride > DB config > JSON),
    // the same value TeamFilter used to seed the candidate pool — so Stage 2 and Stage 3 cannot disagree.
    private static List<string> ResolveUserGroups(AssignmentPipelineContext ctx)
        => string.IsNullOrEmpty(ctx.ResolvedAssigneeGroup) ? [] : [ctx.ResolvedAssigneeGroup];

    // Shared JsonElement-aware readers (see Workflow.JsonPropertyReader).
    private static string? GetPropertyString(Dictionary<string, object> props, string key)
        => JsonPropertyReader.GetString(props, key);

    private static List<string> GetPropertyStringList(Dictionary<string, object> props, string key)
        => JsonPropertyReader.GetStringList(props, key);
}
