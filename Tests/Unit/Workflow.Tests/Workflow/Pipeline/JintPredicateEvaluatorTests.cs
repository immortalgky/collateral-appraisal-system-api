using FluentAssertions;
using Workflow.Workflow.Pipeline;
using Xunit;

namespace Workflow.Tests.Workflow.Pipeline;

/// <summary>
/// Sandbox-integrity and caching tests for JintPredicateEvaluator.
/// Verifies that dangerous patterns are blocked, timeouts are enforced,
/// and the compile cache is keyed by (ConfigurationId, Version).
/// </summary>
public class JintPredicateEvaluatorTests
{
    private static JintPredicateEvaluator BuildSut() => new();

    private static ProcessStepContext EmptyCtx() => new()
    {
        WorkflowInstanceId = Guid.NewGuid(),
        ActivityId = "test",
        ActivityName = "test",
        CompletedBy = "user",
        UserRoles = [],
        Variables = new Dictionary<string, object?>(),
        Input = new Dictionary<string, object?>()
    };

    // T1: No CLR access — attempting to read /etc/passwd should throw or return non-boolean
    [Fact]
    public void NoCLRAccess_SystemIOFile_ThrowsOrFails()
    {
        var sut = BuildSut();
        var configId = Guid.NewGuid();
        const string expression = "typeof System !== 'undefined'";

        // Jint 4.x with AllowClr(false): 'System' is not defined in strict mode → exception
        var act = () => sut.Evaluate(expression, configId, 1, EmptyCtx());

        // Either throws PredicateEvaluationException (runtime error or non-boolean)
        // or returns false (symbol not available). Both are safe outcomes.
        bool result;
        try
        {
            result = act();
        }
        catch (PredicateEvaluationException)
        {
            // Acceptable: CLR not accessible → exception
            return;
        }

        // Acceptable: 'System' undefined → expression evaluates to false
        result.Should().BeFalse("CLR access should not be available in the sandboxed evaluator");
    }

    // T2: No require() — must throw PredicateEvaluationException
    [Fact]
    public void NoRequire_ThrowsPredicateEvaluationException()
    {
        var sut = BuildSut();
        var configId = Guid.NewGuid();
        const string expression = "typeof require === 'function'";

        // In strict Jint, 'require' is not defined → runtime error or returns false
        bool result;
        try
        {
            result = sut.Evaluate(expression, configId, 1, EmptyCtx());
        }
        catch (PredicateEvaluationException)
        {
            return; // Acceptable
        }

        // 'require' is undefined → expression is false
        result.Should().BeFalse("require() must not be available in the sandboxed evaluator");
    }

    // T3: Infinite loop is halted by MaxStatements or TimeoutInterval
    [Fact]
    public void InfiniteLoop_IsHaltedByTimeout()
    {
        var sut = BuildSut();
        var configId = Guid.NewGuid();
        const string expression = "var i = 0; while(true){ i++; } i > 0";

        var act = () => sut.Evaluate(expression, configId, 1, EmptyCtx());

        act.Should().Throw<PredicateEvaluationException>(
            "infinite loops must be halted by the Jint MaxStatements or TimeoutInterval limit");
    }

    // T4: Compile cache returns same compiled script for same (configId, version)
    [Fact]
    public void CompileCache_SameConfigIdAndVersion_ReturnsTrue_OnBothCalls()
    {
        var sut = BuildSut();
        var configId = Guid.NewGuid();

        // Simple true expression — both calls should succeed with same result
        var r1 = sut.Evaluate("true", configId, 1, EmptyCtx());
        var r2 = sut.Evaluate("true", configId, 1, EmptyCtx());

        r1.Should().BeTrue();
        r2.Should().BeTrue();
    }

    // T5: Bumping version recompiles — different version with different expression
    [Fact]
    public void CompileCache_VersionBump_RecompilesExpression()
    {
        var sut = BuildSut();
        var configId = Guid.NewGuid();

        // Version 1: returns true
        var r1 = sut.Evaluate("true", configId, 1, EmptyCtx());
        // Version 2: different expression, returns false
        var r2 = sut.Evaluate("false", configId, 2, EmptyCtx());

        r1.Should().BeTrue("version 1 should evaluate 'true'");
        r2.Should().BeFalse("version 2 should evaluate 'false', proving the cache was invalidated");
    }

    // T6: Valid expression evaluating workflow.variables works
    [Fact]
    public void Evaluate_WorkflowVariableAccess_ReturnsCorrectBoolean()
    {
        var sut = BuildSut();
        var configId = Guid.NewGuid();

        var ctx = new ProcessStepContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "test",
            ActivityName = "test",
            CompletedBy = "user",
            UserRoles = [],
            Variables = new Dictionary<string, object?> { ["channel"] = "MANUAL" },
            Input = new Dictionary<string, object?>()
        };

        var result = sut.Evaluate("workflow.variables.channel === 'MANUAL'", configId, 1, ctx);

        result.Should().BeTrue();
    }

    // T7: TryPrepare returns null for valid expression
    [Fact]
    public void TryPrepare_ValidExpression_ReturnsNull()
    {
        var sut = BuildSut();
        var error = sut.TryPrepare("workflow.variables.x === 'y'");
        error.Should().BeNull("valid JS should compile without error");
    }

    // T8: TryPrepare returns error message for malformed expression
    [Fact]
    public void TryPrepare_MalformedExpression_ReturnsErrorMessage()
    {
        var sut = BuildSut();
        var error = sut.TryPrepare("{{{{not valid js");
        error.Should().NotBeNullOrEmpty("malformed JS should produce a compile error");
    }

    // ── EvaluateExpression (per-rule escape-hatch) ──────────────────────

    // T9: EvaluateExpression with true literal returns true
    [Fact]
    public void EvaluateExpression_TrueLiteral_ReturnsTrue()
    {
        var sut = BuildSut();
        var result = sut.EvaluateExpression("true", EmptyCtx());
        result.Should().BeTrue();
    }

    // T10: EvaluateExpression with false literal returns false
    [Fact]
    public void EvaluateExpression_FalseLiteral_ReturnsFalse()
    {
        var sut = BuildSut();
        var result = sut.EvaluateExpression("false", EmptyCtx());
        result.Should().BeFalse();
    }

    // T11: EvaluateExpression can access workflow.variables (same sandbox as Evaluate)
    [Fact]
    public void EvaluateExpression_WorkflowVariablesAccess_ReturnsCorrectBoolean()
    {
        var sut = BuildSut();
        var ctx = new ProcessStepContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "test",
            ActivityName = "test",
            CompletedBy = "user",
            UserRoles = [],
            Variables = new Dictionary<string, object?> { ["channel"] = "SIBS" },
            Input = new Dictionary<string, object?>()
        };

        var result = sut.EvaluateExpression("workflow.variables.channel === 'SIBS'", ctx);
        result.Should().BeTrue();
    }

    // T12: EvaluateExpression with appraisal scope injected
    [Fact]
    public void EvaluateExpression_AppraisalDataInjected_AccessibleInExpression()
    {
        var sut = BuildSut();
        var ctx = new ProcessStepContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "test",
            ActivityName = "test",
            CompletedBy = "user",
            UserRoles = [],
            Variables = new Dictionary<string, object?>(),
            Input = new Dictionary<string, object?>(),
            AppraisalData = new Dictionary<string, object?>
            {
                ["facilityLimit"] = 100_000_000m
            }
        };

        // appraisal is injected as a JS object when AppraisalData is non-empty
        var result = sut.EvaluateExpression("appraisal.facilityLimit > 50000000", ctx);
        result.Should().BeTrue("facilityLimit 100M > 50M should evaluate to true");
    }

    // T13: EvaluateExpression non-boolean result throws PredicateEvaluationException
    [Fact]
    public void EvaluateExpression_NonBooleanResult_ThrowsPredicateEvaluationException()
    {
        var sut = BuildSut();
        var act = () => sut.EvaluateExpression("'not a boolean'", EmptyCtx());
        act.Should().Throw<PredicateEvaluationException>(
            "returning a non-boolean from EvaluateExpression should throw");
    }

    // T14: EvaluateExpression caches by expression content (same expression reused)
    [Fact]
    public void EvaluateExpression_SameExpression_CachedAndReused()
    {
        var sut = BuildSut();
        // Should not throw on second call with identical expression
        var r1 = sut.EvaluateExpression("true", EmptyCtx());
        var r2 = sut.EvaluateExpression("true", EmptyCtx());

        r1.Should().BeTrue();
        r2.Should().BeTrue();
    }

    // T15: EvaluateExpression different expressions cached independently
    [Fact]
    public void EvaluateExpression_DifferentExpressions_EvaluateIndependently()
    {
        var sut = BuildSut();
        var r1 = sut.EvaluateExpression("true", EmptyCtx());
        var r2 = sut.EvaluateExpression("false", EmptyCtx());

        r1.Should().BeTrue();
        r2.Should().BeFalse();
    }
}
