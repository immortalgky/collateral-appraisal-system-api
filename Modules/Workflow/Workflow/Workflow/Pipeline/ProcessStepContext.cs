using System.Text.Json;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Typed context passed to each process step during activity completion.
/// Steps access workflow state through the read-only bags; no aggregate exposure.
/// </summary>
public sealed record ProcessStepContext
{
    public Guid WorkflowInstanceId { get; init; }

    /// <summary>The workflow activity execution id for this completion attempt.</summary>
    public Guid WorkflowActivityExecutionId { get; init; }

    public string ActivityId { get; init; } = default!;
    public string ActivityName { get; init; } = default!;
    public string CompletedBy { get; init; } = default!;

    /// <summary>Roles held by the completing user.</summary>
    public IReadOnlyList<string> UserRoles { get; init; } = [];

    /// <summary>Workflow-level variables (read-only view).</summary>
    public IReadOnlyDictionary<string, object?> Variables { get; init; } =
        new Dictionary<string, object?>();

    /// <summary>Input provided with this completion call (read-only view).</summary>
    public IReadOnlyDictionary<string, object?> Input { get; init; } =
        new Dictionary<string, object?>();

    public CancellationToken CancellationToken { get; init; }

    /// <summary>Raw ParametersJson from the config row — accessed via GetParameters&lt;T&gt;().</summary>
    public string? ParametersJson { get; init; }

    // ── Legacy helpers kept for steps that still reference them ───────────

    /// <summary>
    /// Workflow correlation ID (requestId). Derived from Variables["correlationId"] when present.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>
    /// Appraisal ID from Variables["appraisalId"]. Null before appraisal creation.
    /// </summary>
    public Guid? AppraisalId { get; init; }

    // ── B5: Explicit variable mutation API ────────────────────────────────

    /// <summary>
    /// Pending variable writes collected during the pipeline run.
    /// The pipeline merges these into the WorkflowInstance after Actions succeed.
    /// Steps must use SetVariable() — do not cast Variables to IDictionary.
    /// </summary>
    public Dictionary<string, object?> PendingVariableWrites { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Queues a variable write. The value is visible to subsequent steps reading Variables
    /// only after the pipeline persists the merged result.
    /// For idempotency guards that need to read the flag within the same run,
    /// check PendingVariableWrites first, then fall back to Variables.
    /// </summary>
    public void SetVariable(string key, object? value)
    {
        PendingVariableWrites[key] = value;
    }

    // ── Typed parameter accessor ───────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Deserialises the step's ParametersJson into a typed record.
    /// Returns a default instance when ParametersJson is null/empty.
    /// </summary>
    public TParams GetParameters<TParams>() where TParams : new()
    {
        if (string.IsNullOrWhiteSpace(ParametersJson))
            return new TParams();

        return JsonSerializer.Deserialize<TParams>(ParametersJson, _jsonOptions) ?? new TParams();
    }
}
