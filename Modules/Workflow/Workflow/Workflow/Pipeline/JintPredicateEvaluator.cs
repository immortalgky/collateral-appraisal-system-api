using System.Collections.Concurrent;
using System.Text.Json;
using Acornima.Ast;
using Jint;
using Jint.Native;
using Microsoft.Extensions.Logging;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Evaluates sandboxed JavaScript RunIfExpressions using Jint 4.x.
/// Compile cache is keyed by (ConfigurationId, Version) so a version bump auto-invalidates.
/// Each evaluation creates a fresh Engine instance (stateless, thread-safe by design).
/// </summary>
public sealed class JintPredicateEvaluator(ILogger<JintPredicateEvaluator> logger) : IPredicateEvaluator
{
    // Cache of compiled scripts keyed by (configId, version)
    private readonly ConcurrentDictionary<(Guid, int), Prepared<Script>> _cache = new();

    public bool Evaluate(
        string expression,
        Guid configurationId,
        int configurationVersion,
        ProcessStepContext ctx)
    {
        var prepared = _cache.GetOrAdd(
            (configurationId, configurationVersion),
            _ => PrepareScript(expression));

        var engine = BuildEngine();
        InjectContext(engine, ctx);

        JsValue result;
        try
        {
            result = engine.Evaluate(prepared);
        }
        catch (Exception ex)
        {
            throw new PredicateEvaluationException(
                $"RunIfExpression threw at runtime: {ex.Message}", ex);
        }

        if (result.IsBoolean())
            return result.AsBoolean();

        throw new PredicateEvaluationException(
            $"RunIfExpression must return a boolean but returned '{result.Type}'");
    }

    public string? TryPrepare(string expression)
    {
        try
        {
            PrepareScript(expression);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static Prepared<Script> PrepareScript(string expression)
    {
        // PrepareScript is a static factory on Jint.Engine
        return Jint.Engine.PrepareScript(expression);
    }

    private static Jint.Engine BuildEngine() => new(opts =>
    {
        opts.LimitRecursion(32);
        opts.TimeoutInterval(TimeSpan.FromMilliseconds(100));
        opts.MaxStatements(1000);
        opts.Strict(true);
        // No AllowClr = CLR types inaccessible by default in Jint 4.x
    });

    private static void InjectContext(Jint.Engine engine, ProcessStepContext ctx)
    {
        // Build plain dictionaries for context objects and inject via SetValue(name, object)
        // Jint will serialize them as JS objects (read-only globals in strict mode)
        var workflowContext = new Dictionary<string, object?>
        {
            ["variables"] = FlattenDict(ctx.Variables)
        };
        engine.SetValue("workflow", workflowContext);

        var activityContext = new Dictionary<string, object?>
        {
            ["id"] = ctx.ActivityId ?? string.Empty,
            ["name"] = ctx.ActivityName ?? string.Empty,
            ["input"] = FlattenDict(ctx.Input)
        };
        engine.SetValue("activity", activityContext);

        var userContext = new Dictionary<string, object?>
        {
            ["id"] = ctx.CompletedBy ?? string.Empty,
            ["roles"] = ctx.UserRoles.ToArray()
        };
        engine.SetValue("user", userContext);
    }

    /// <summary>
    /// Flattens a read-only dictionary to a plain dictionary that Jint can serialize.
    /// Resolves JsonElement values to their native equivalents.
    /// </summary>
    private static Dictionary<string, object?> FlattenDict(IReadOnlyDictionary<string, object?> dict)
    {
        var result = new Dictionary<string, object?>(dict.Count);
        foreach (var kv in dict)
            result[kv.Key] = FlattenValue(kv.Value);
        return result;
    }

    private static object? FlattenValue(object? value)
    {
        return value switch
        {
            JsonElement je => JsonElementToNative(je),
            _ => value
        };
    }

    private static object? JsonElementToNative(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => (object)true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToNative(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(JsonElementToNative)
                .ToArray(),
            _ => element.GetRawText()
        };
    }
}
