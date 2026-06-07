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
/// Progress events are pushed via IActivityProgressReporter (no-op by default).
/// </summary>
public sealed class ActivityProcessPipeline : IActivityProcessPipeline
{
    private readonly WorkflowDbContext _dbContext;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IPredicateEvaluator _predicateEvaluator;
    private readonly IActivityProcessExecutionSink _executionSink;
    private readonly IActivityProgressReporter _progressReporter;
    private readonly ILogger<ActivityProcessPipeline> _logger;
    private readonly Dictionary<string, IActivityProcessStep> _stepsByName;

    public ActivityProcessPipeline(
        WorkflowDbContext dbContext,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IPredicateEvaluator predicateEvaluator,
        IEnumerable<IActivityProcessStep> steps,
        IActivityProcessExecutionSink executionSink,
        IActivityProgressReporter progressReporter,
        ILogger<ActivityProcessPipeline> logger)
    {
        _dbContext = dbContext;
        _workflowInstanceRepository = workflowInstanceRepository;
        _predicateEvaluator = predicateEvaluator;
        _executionSink = executionSink;
        _progressReporter = progressReporter;
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
        IReadOnlyCollection<string> acknowledgedWarningTokens,
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

        // Build StepInfo list from ordered configs for the progress reporter.
        // DisplayName is resolved from the registered step descriptor; falls back to StepName.
        var stepInfos = configs.Select(c =>
        {
            var displayName = _stepsByName.TryGetValue(c.ProcessorName, out var s)
                ? s.Descriptor.DisplayName
                : c.StepName;
            return new StepInfo(c.ProcessorName, displayName, c.SortOrder, c.Kind.ToString());
        }).ToList();

        await TryReportAsync(() =>
            _progressReporter.PipelineStarted(
                workflowActivityExecutionId, activityName, stepInfos, completedBy, ct));

        // Load workflow instance for Variables + CorrelationId
        var workflowInstance = await _workflowInstanceRepository.GetByIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        var variables = BuildVariables(workflowInstance.Variables);
        var correlationId = ResolveCorrelationId(workflowInstance.CorrelationId, workflowInstanceId);
        var appraisalId = ResolveAppraisalId(workflowInstance.Variables);

        // Resolve the completing decision's movement (F/B/C) so forward-only validations can
        // gate on `activity.movement === 'F'` and skip on route-back / cancel.
        var movement = ResolveMovement(workflowInstance, activityName, input);

        // B5: Collect pending variable writes separately so mutations survive the pipeline.
        var pendingVariableWrites = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var baseCtx = new ProcessStepContext
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowDefinitionId = workflowInstance.WorkflowDefinitionId,
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            ActivityId = activityName,    // W9: populated from activityName (correct for lookup)
            ActivityName = activityName,
            CompletedBy = completedBy,
            Movement = movement,
            UserRoles = userRoles,         // W10: populated from caller (ICurrentUserService.Roles)
            Variables = variables,
            Input = input,
            CancellationToken = ct,
            CorrelationId = correlationId,
            AppraisalId = appraisalId,
            PendingVariableWrites = pendingVariableWrites
        };

        var traces = new List<ActivityProcessExecution>();
        var errorFailures = new List<StepFailure>();
        var warningFailures = new List<StepFailure>();
        // Map ackToken → its Warning-severity trace row, so acknowledged warnings can be stamped.
        var warningTracesByToken = new Dictionary<string, ActivityProcessExecution>(StringComparer.Ordinal);

        // ── Validations phase (collect-all) ───────────────────────────────

        var validationConfigs = configs.Where(c => c.Kind == StepKind.Validation).ToList();
        foreach (var config in validationConfigs)
        {
            var stepInfo = BuildStepInfo(config);
            await TryReportAsync(() =>
                _progressReporter.StepStarted(workflowActivityExecutionId, stepInfo, completedBy, ct));

            var ctx = WithParameters(baseCtx, config.ParametersJson);
            var (trace, failure) = await ExecuteStepAsync(
                config, ctx, workflowInstanceId, workflowActivityExecutionId, ct);

            await TryReportAsync(() =>
                _progressReporter.StepFinished(
                    workflowActivityExecutionId, stepInfo,
                    trace.Outcome.ToString(), trace.DurationMs, completedBy, ct));

            traces.Add(trace);

            if (failure is not null)
            {
                // Effective severity: hard failures (errored/not-found/expression-error) are always
                // Error (fail-closed); only a clean business Failed honours the configured severity.
                if (failure.Severity == StepSeverity.Warning && failure.AckToken is not null)
                {
                    warningFailures.Add(failure);
                    warningTracesByToken[failure.AckToken] = trace;
                }
                else
                {
                    errorFailures.Add(failure);
                }
            }
        }

        // ── Actions phase (stop-on-first) ─────────────────────────────────

        var actionConfigs = configs.Where(c => c.Kind == StepKind.Action).ToList();

        // Any blocking error → fail; no mutation (today's path).
        if (errorFailures.Count > 0)
        {
            foreach (var config in actionConfigs)
            {
                traces.Add(TraceSkipped(
                    workflowInstanceId, workflowActivityExecutionId,
                    config, SkipReason.ValidationPhaseFailed));
            }

            await _executionSink.PersistAsync(traces, ct);
            await TryReportAsync(() =>
                _progressReporter.PipelineFinished(
                    workflowActivityExecutionId, "ValidationsFailed", completedBy, ct));
            return PipelineResult.ValidationsFailed(errorFailures);
        }

        // No errors, but ≥1 warning: proceed only if EVERY warning token was acknowledged.
        if (warningFailures.Count > 0)
        {
            var ackSet = acknowledgedWarningTokens as ISet<string>
                         ?? new HashSet<string>(acknowledgedWarningTokens, StringComparer.Ordinal);

            var unacknowledged = warningFailures
                .Where(w => w.AckToken is null || !ackSet.Contains(w.AckToken))
                .ToList();

            if (unacknowledged.Count > 0)
            {
                // Soft gate: surface warnings, run no Actions, mutate nothing.
                foreach (var config in actionConfigs)
                {
                    traces.Add(TraceSkipped(
                        workflowInstanceId, workflowActivityExecutionId,
                        config, SkipReason.WarningsPending));
                }

                await _executionSink.PersistAsync(traces, ct);
                await TryReportAsync(() =>
                    _progressReporter.PipelineFinished(
                        workflowActivityExecutionId, "WarningsPending", completedBy, ct));
                return PipelineResult.WarningsPending(warningFailures);
            }

            // All warnings acknowledged — stamp the audit trail and continue to Actions.
            foreach (var w in warningFailures)
            {
                if (w.AckToken is not null
                    && warningTracesByToken.TryGetValue(w.AckToken, out var wTrace))
                {
                    wTrace.MarkAcknowledged(completedBy, w.AckToken);
                }
            }
        }

        StepFailure? actionFailure = null;
        foreach (var config in actionConfigs)
        {
            var stepInfo = BuildStepInfo(config);
            await TryReportAsync(() =>
                _progressReporter.StepStarted(workflowActivityExecutionId, stepInfo, completedBy, ct));

            var ctx = WithParameters(baseCtx, config.ParametersJson);
            var (trace, failure) = await ExecuteStepAsync(
                config, ctx, workflowInstanceId, workflowActivityExecutionId, ct);

            await TryReportAsync(() =>
                _progressReporter.StepFinished(
                    workflowActivityExecutionId, stepInfo,
                    trace.Outcome.ToString(), trace.DurationMs, completedBy, ct));

            traces.Add(trace);

            if (failure is not null)
            {
                actionFailure = failure;
                break;
            }
        }

        await _executionSink.PersistAsync(traces, ct);

        if (actionFailure is not null)
        {
            await TryReportAsync(() =>
                _progressReporter.PipelineFinished(
                    workflowActivityExecutionId, "ActionFailed", completedBy, ct));
            return PipelineResult.ActionFailed(actionFailure);
        }

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

        await TryReportAsync(() =>
            _progressReporter.PipelineFinished(
                workflowActivityExecutionId, "Success", completedBy, ct));

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

        // Resolve step from DI dictionary.
        // Fail-closed (Part D): an unknown ProcessorName must BLOCK, never silently pass —
        // a misconfigured pipeline should not let an activity complete unchecked.
        if (!_stepsByName.TryGetValue(stepName, out var step))
        {
            _logger.LogError(
                "Step '{ProcessorName}' (StepName='{StepName}') not found in registered steps — " +
                "this indicates a config/DI mismatch. Blocking completion (fail-closed).",
                config.ProcessorName, config.StepName);
            var unknownTrace = ActivityProcessExecution.Record(
                workflowInstanceId, workflowActivityExecutionId,
                config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                config.RunIfExpression, config.ParametersJson,
                StepOutcome.Errored, null, 0, "Step not found in registered steps",
                StepSeverity.Error);
            return (unknownTrace,
                new StepFailure(stepName, "StepNotFound",
                    $"Configured step '{stepName}' is not registered.", StepSeverity.Error));
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
                    StepOutcome.Passed, null, durationMs, null, config.Severity);
                break;

            case ProcessStepResult.Failed f:
                // A clean business failure honours the configured severity (Error blocks;
                // Warning is acknowledge-to-continue). Compute a stable ackToken for warnings.
                var severity = config.Severity;
                var token = severity == StepSeverity.Warning
                    ? ComputeAckToken(stepName, severity, f.Message, config.Version)
                    : null;
                _logger.LogWarning(
                    "Step '{StepName}' failed ({Severity}): [{ErrorCode}] {Message}",
                    stepName, severity, f.ErrorCode, f.Message);
                trace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Failed, null, durationMs, TrimMessage(f.Message), severity);
                failure = new StepFailure(stepName, f.ErrorCode, f.Message, severity, token);
                break;

            case ProcessStepResult.Errored e:
                // Fail-closed: an unhandled exception always blocks, regardless of config severity.
                _logger.LogError(e.Exception,
                    "Step '{StepName}' threw an unhandled exception", stepName);
                trace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Errored, null, durationMs, TrimMessage(e.Exception.Message),
                    StepSeverity.Error);
                failure = new StepFailure(stepName, "UnhandledError", e.Exception.Message, StepSeverity.Error);
                break;

            default:
                trace = ActivityProcessExecution.Record(
                    workflowInstanceId, workflowActivityExecutionId,
                    config.Id, config.Version, stepName, config.Kind, config.SortOrder,
                    config.RunIfExpression, config.ParametersJson,
                    StepOutcome.Errored, null, durationMs, "Unknown result type", StepSeverity.Error);
                failure = new StepFailure(stepName, "UnknownResult", "Unknown result type", StepSeverity.Error);
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

    /// <summary>
    /// Resolves the movement ("F"/"B"/"C") of the completing decision by matching the decision
    /// value in <paramref name="input"/> against the activity's <c>actions[].movement</c> in the
    /// workflow definition. Returns "F" when no decision is present or no action matches, so
    /// activities without an explicit decision keep validating forward.
    /// </summary>
    private static string ResolveMovement(
        global::Workflow.Workflow.Models.WorkflowInstance instance,
        string activityName,
        IReadOnlyDictionary<string, object?> input)
    {
        // The completing decision is sent under the BARE key "decisionTaken" — that is what the
        // caller/FE sends and what TaskActivity reads (TaskActivity.cs:183). The activity-prefixed
        // form "{normalizedActivityId}_decisionTaken" is only written into Variables LATER, during
        // ResumeWorkflow (which runs after this pipeline), so read the bare key first and fall back
        // to the prefixed form defensively.
        if (!input.TryGetValue("decisionTaken", out var decisionRaw) || decisionRaw is null)
        {
            var prefixedKey = activityName.Replace("-", "_") + "_decisionTaken";
            input.TryGetValue(prefixedKey, out decisionRaw);
        }
        if (decisionRaw is null)
            return "F";

        var decision = decisionRaw is JsonElement dje
            ? (dje.ValueKind == JsonValueKind.String ? dje.GetString() : dje.ToString())
            : decisionRaw.ToString();
        if (string.IsNullOrWhiteSpace(decision))
            return "F";

        var properties = global::Workflow.AssigneeSelection.Pipeline.ActivityPropertiesExtractor.Extract(instance, activityName);
        if (!properties.TryGetValue("actions", out var actionsObj)
            || actionsObj is not JsonElement actions
            || actions.ValueKind != JsonValueKind.Array)
            return "F";

        foreach (var action in actions.EnumerateArray())
        {
            if (!action.TryGetProperty("value", out var valEl)) continue;
            var value = valEl.ValueKind == JsonValueKind.String ? valEl.GetString() : valEl.ToString();
            if (!string.Equals(value, decision, StringComparison.OrdinalIgnoreCase)) continue;

            if (!action.TryGetProperty("movement", out var movEl)) return "F";
            var movement = movEl.ValueKind == JsonValueKind.String ? movEl.GetString() : null;
            return movement?.ToUpperInvariant() switch
            {
                "B" => "B",
                "C" => "C",
                _ => "F"
            };
        }

        return "F";
    }

    /// <summary>
    /// Stable identity of a warning so an acknowledgement is bound to the exact warning shown.
    /// A changed message or config version yields a different token, forcing re-acknowledgement.
    /// </summary>
    private static string ComputeAckToken(string stepName, StepSeverity severity, string message, int configVersion)
    {
        var raw = $"{stepName}:{severity}:{message}:{configVersion}";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Builds a <see cref="StepInfo"/> for a config row, resolving DisplayName from the
    /// registered step descriptor when available (falls back to config.StepName).
    /// </summary>
    private StepInfo BuildStepInfo(ActivityProcessConfiguration config)
    {
        var displayName = _stepsByName.TryGetValue(config.ProcessorName, out var step)
            ? step.Descriptor.DisplayName
            : config.StepName;
        return new StepInfo(config.ProcessorName, displayName, config.SortOrder, config.Kind.ToString());
    }

    /// <summary>
    /// Invokes a reporter method and swallows any exception so it can never throw into
    /// the pipeline transaction. Errors are logged at Warning level.
    /// </summary>
    private async Task TryReportAsync(Func<Task> report)
    {
        try
        {
            await report();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Progress reporter threw; pipeline unaffected");
        }
    }
}
