using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Orchestrates the Validations-then-Actions pipeline for an activity completion.
/// Phase 1: All Validation steps run; failures are collected (collect-all).
/// Phase 2: If Validations pass, Action steps run; first failure halts (stop-on-first).
/// Trace rows are persisted via IActivityProcessExecutionSink on a separate connection
/// so they survive an outer transaction rollback.
/// </summary>
public sealed class ActivityProcessPipeline : IActivityProcessPipeline
{
    private readonly WorkflowDbContext _dbContext;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IPredicateEvaluator _predicateEvaluator;
    private readonly IActivityProcessExecutionSink _executionSink;
    private readonly ILogger<ActivityProcessPipeline> _logger;
    private readonly Dictionary<string, IActivityProcessStep> _stepsByName;

    public ActivityProcessPipeline(
        WorkflowDbContext dbContext,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IPredicateEvaluator predicateEvaluator,
        IEnumerable<IActivityProcessStep> steps,
        IActivityProcessExecutionSink executionSink,
        ILogger<ActivityProcessPipeline> logger)
    {
        _dbContext = dbContext;
        _workflowInstanceRepository = workflowInstanceRepository;
        _predicateEvaluator = predicateEvaluator;
        _executionSink = executionSink;
        _logger = logger;

        _stepsByName = steps.ToDictionary(
            s => s.Descriptor.Name, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<PipelineResult> ExecuteAsync(
        Guid workflowInstanceId,
        Guid workflowActivityExecutionId,
        string activityName,
        string completedBy,
        IReadOnlyList<string> userRoles,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken ct)
    {
        // Load active configs ordered: Validations first, then Actions, then by SortOrder
        var configs = await _dbContext.ActivityProcessConfigurations
            .Where(c => c.ActivityName == activityName && c.IsActive)
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ToListAsync(ct);

        if (configs.Count == 0)
        {
            _logger.LogDebug("No active process configurations for activity {ActivityName}", activityName);
            return PipelineResult.Success();
        }

        // Load workflow instance for Variables + CorrelationId
        var workflowInstance = await _workflowInstanceRepository.GetByIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        var variables = BuildVariables(workflowInstance.Variables);
        var correlationId = ResolveCorrelationId(workflowInstance.CorrelationId, workflowInstanceId);
        var appraisalId = ResolveAppraisalId(workflowInstance.Variables);

        // B5: Collect pending variable writes separately so mutations survive the pipeline.
        var pendingVariableWrites = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var baseCtx = new ProcessStepContext
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            ActivityId = activityName,    // W9: populated from activityName (correct for lookup)
            ActivityName = activityName,
            CompletedBy = completedBy,
            UserRoles = userRoles,         // W10: populated from caller (ICurrentUserService.Roles)
            Variables = variables,
            Input = input,
            CancellationToken = ct,
            CorrelationId = correlationId,
            AppraisalId = appraisalId,
            PendingVariableWrites = pendingVariableWrites
        };

        var traces = new List<ActivityProcessExecution>();
        var validationFailures = new List<StepFailure>();
        var validationsPassed = true;

        // ── Validations phase (collect-all) ───────────────────────────────

        var validationConfigs = configs.Where(c => c.Kind == StepKind.Validation).ToList();
        foreach (var config in validationConfigs)
        {
            var ctx = WithParameters(baseCtx, config.ParametersJson);
            var (trace, failure) = await ExecuteStepAsync(
                config, ctx, workflowInstanceId, workflowActivityExecutionId, ct);

            traces.Add(trace);

            if (failure is not null)
            {
                validationFailures.Add(failure);
                validationsPassed = false;
            }
        }

        // ── Actions phase (stop-on-first) ─────────────────────────────────

        var actionConfigs = configs.Where(c => c.Kind == StepKind.Action).ToList();

        if (!validationsPassed)
        {
            foreach (var config in actionConfigs)
            {
                traces.Add(TraceSkipped(
                    workflowInstanceId, workflowActivityExecutionId,
                    config, SkipReason.ValidationPhaseFailed));
            }

            await _executionSink.PersistAsync(traces, ct);
            return PipelineResult.ValidationsFailed(validationFailures);
        }

        StepFailure? actionFailure = null;
        foreach (var config in actionConfigs)
        {
            var ctx = WithParameters(baseCtx, config.ParametersJson);
            var (trace, failure) = await ExecuteStepAsync(
                config, ctx, workflowInstanceId, workflowActivityExecutionId, ct);

            traces.Add(trace);

            if (failure is not null)
            {
                actionFailure = failure;
                break;
            }
        }

        await _executionSink.PersistAsync(traces, ct);

        if (actionFailure is not null)
            return PipelineResult.ActionFailed(actionFailure);

        // B5: Merge pending variable writes into the workflow instance and persist.
        if (pendingVariableWrites.Count > 0)
        {
            var fresh = await _workflowInstanceRepository.GetByIdAsync(workflowInstanceId, ct)
                        ?? throw new InvalidOperationException(
                            $"Workflow instance {workflowInstanceId} not found for variable merge");

            // Convert nullable values to non-nullable for the UpdateVariables contract.
            var mergeDict = pendingVariableWrites
                .Where(kv => kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value!);

            fresh.UpdateVariables(mergeDict);
            await _workflowInstanceRepository.UpdateAsync(fresh, ct);
        }

        return PipelineResult.Success();
    }

    // ── Step execution ────────────────────────────────────────────────────

    private async Task<(ActivityProcessExecution Trace, StepFailure? Failure)> ExecuteStepAsync(
        ActivityProcessConfiguration config,
        ProcessStepContext ctx,
        Guid workflowInstanceId,
        Guid workflowActivityExecutionId,
        CancellationToken ct)
    {
        // B2: Resolve by ProcessorName (the canonical Descriptor.Name key).
        // StepName is the human-readable label; ProcessorName is the stable DI key.
        var stepName = config.ProcessorName;

        // Resolve step from DI dictionary
        if (!_stepsByName.TryGetValue(stepName, out var step))
        {
            _logger.LogError(
                "Step '{ProcessorName}' (StepName='{StepName}') not found in registered steps — " +
                "this indicates a config/DI mismatch. Aborting pipeline.",
                config.ProcessorName, config.StepName);
            var unknownTrace = ActivityProcessExecution.Record(
                workflowInstanceId, workflowActivityExecutionId,
                config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                config.RunIfExpression, config.ParametersJson,
                StepOutcome.Skipped, null, 0, "Step not found in registered steps");
            return (unknownTrace, null);
        }

        // Evaluate RunIfExpression
        if (!string.IsNullOrWhiteSpace(config.RunIfExpression))
        {
            bool shouldRun;
            try
            {
                shouldRun = _predicateEvaluator.Evaluate(
                    config.RunIfExpression, config.Id, config.Version, ctx);
            }
            catch (PredicateEvaluationException pex)
            {
                _logger.LogWarning(pex,
                    "RunIfExpression error for step '{StepName}', halting pipeline", stepName);
                var errorTrace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Skipped, SkipReason.ExpressionError,
                    0, TrimMessage(pex.Message));
                return (errorTrace,
                    new StepFailure(stepName, "ExpressionError", pex.Message));
            }

            if (!shouldRun)
            {
                return (TraceSkipped(workflowInstanceId, workflowActivityExecutionId,
                    config, SkipReason.RunIfFalse), null);
            }
        }

        // Execute the step
        var sw = Stopwatch.StartNew();
        ProcessStepResult result;
        try
        {
            result = await step.ExecuteAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            result = ProcessStepResult.Error(ex);
        }
        sw.Stop();

        var durationMs = (int)sw.ElapsedMilliseconds;

        ActivityProcessExecution trace;
        StepFailure? failure = null;

        switch (result)
        {
            case ProcessStepResult.Passed:
                trace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Passed, null, durationMs, null);
                break;

            case ProcessStepResult.Failed f:
                _logger.LogWarning(
                    "Step '{StepName}' failed: [{ErrorCode}] {Message}", stepName, f.ErrorCode, f.Message);
                trace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Failed, null, durationMs, TrimMessage(f.Message));
                failure = new StepFailure(stepName, f.ErrorCode, f.Message);
                break;

            case ProcessStepResult.Errored e:
                _logger.LogError(e.Exception,
                    "Step '{StepName}' threw an unhandled exception", stepName);
                trace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Errored, null, durationMs, TrimMessage(e.Exception.Message));
                failure = new StepFailure(stepName, "UnhandledError", e.Exception.Message);
                break;

            default:
                trace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Errored, null, durationMs, "Unknown result type");
                failure = new StepFailure(stepName, "UnknownResult", "Unknown result type");
                break;
        }

        return (trace, failure);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static ActivityProcessExecution TraceSkipped(
        Guid workflowInstanceId,
        Guid workflowActivityExecutionId,
        ActivityProcessConfiguration config,
        SkipReason reason)
    {
        return ActivityProcessExecution.Record(
            workflowInstanceId, workflowActivityExecutionId,
            config.Id, config.Version,
            config.ProcessorName,  // B2: use ProcessorName (canonical key), not StepName
            config.Kind, config.SortOrder,
            config.RunIfExpression, config.ParametersJson,
            StepOutcome.Skipped, reason, 0, null);
    }

    private static ProcessStepContext WithParameters(ProcessStepContext baseCtx, string? parametersJson)
    {
        return baseCtx with { ParametersJson = parametersJson };
    }

    private static IReadOnlyDictionary<string, object?> BuildVariables(Dictionary<string, object>? raw)
    {
        if (raw is null) return new Dictionary<string, object?>();
        var result = new Dictionary<string, object?>(raw.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
            result[kv.Key] = kv.Value;
        return result;
    }

    private static Guid ResolveCorrelationId(string? raw, Guid fallback)
    {
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out var id))
            return fallback;
        return id;
    }

    private static Guid? ResolveAppraisalId(Dictionary<string, object>? variables)
    {
        if (variables is null || !variables.TryGetValue("appraisalId", out var aidObj))
            return null;

        return aidObj switch
        {
            Guid g => g,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            JsonElement je when je.ValueKind == JsonValueKind.String
                && Guid.TryParse(je.GetString(), out var jp) => jp,
            _ => null
        };
    }

    private static string TrimMessage(string msg) =>
        msg.Length > 1000 ? msg[..1000] : msg;
}
