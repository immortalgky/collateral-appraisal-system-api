using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Pipeline;
using Xunit;

namespace Workflow.Tests.Workflow.Pipeline;

public class AppraisalCreationTriggerEvaluatorTests
{
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<AppraisalCreationTriggerEvaluator> _logger;

    public AppraisalCreationTriggerEvaluatorTests()
    {
        _expressionEvaluator = new ExpressionEvaluator();
        _logger = Substitute.For<ILogger<AppraisalCreationTriggerEvaluator>>();
    }

    // ── EvaluateConfig: condition evaluation ──

    [Fact]
    public void EvaluateConfig_NullParameters_ReturnsTrue()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "ONLINE" };

        evaluator.EvaluateConfig(null, variables).Should().BeTrue();
    }

    [Fact]
    public void EvaluateConfig_EmptyParameters_ReturnsTrue()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object>();

        evaluator.EvaluateConfig("", variables).Should().BeTrue();
    }

    [Fact]
    public void EvaluateConfig_ConditionMatches_ReturnsTrue()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "ONLINE" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel != 'MANUAL'"}""", variables);

        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateConfig_ConditionDoesNotMatch_ReturnsFalse()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "MANUAL" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel != 'MANUAL'"}""", variables);

        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateConfig_ManualConditionMatches_ReturnsTrue()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "MANUAL" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel == 'MANUAL'"}""", variables);

        result.Should().BeTrue();
    }

    // ── EvaluateConfig: requireDecision ──

    [Fact]
    public void EvaluateConfig_RequireDecision_MatchingInput_ReturnsTrue()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "MANUAL" };
        var input = new Dictionary<string, object> { ["decisionTaken"] = "P" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}""",
            variables, input);

        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateConfig_RequireDecision_NonMatchingInput_ReturnsFalse()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "MANUAL" };
        var input = new Dictionary<string, object> { ["decisionTaken"] = "R" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}""",
            variables, input);

        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateConfig_RequireDecision_NoInput_ReturnsFalse()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "MANUAL" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}""",
            variables, input: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateConfig_RequireDecision_MissingDecisionField_ReturnsFalse()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "MANUAL" };
        var input = new Dictionary<string, object> { ["otherField"] = "P" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}""",
            variables, input);

        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateConfig_CustomDecisionField_UsesCorrectField()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object> { ["channel"] = "MANUAL" };
        var input = new Dictionary<string, object> { ["action"] = "APPROVE" };

        var result = evaluator.EvaluateConfig(
            """{"condition": "channel == 'MANUAL'", "requireDecision": "APPROVE", "decisionField": "action"}""",
            variables, input);

        result.Should().BeTrue();
    }

    // ── EvaluateConfig: invalid JSON ──

    [Fact]
    public void EvaluateConfig_InvalidJson_ReturnsFalse()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object>();

        var result = evaluator.EvaluateConfig("not json", variables);

        result.Should().BeFalse();
    }

    // ── EvaluateConfig: no condition property ──

    [Fact]
    public void EvaluateConfig_NoConditionProperty_ReturnsTrue()
    {
        var evaluator = BuildEvaluator();
        var variables = new Dictionary<string, object>();

        var result = evaluator.EvaluateConfig("""{"someOtherProp": "value"}""", variables);

        result.Should().BeTrue();
    }

    // ── Helpers ──

    private AppraisalCreationTriggerEvaluator BuildEvaluator()
    {
        // Use a null dbContext since EvaluateConfig doesn't query the database
        return new AppraisalCreationTriggerEvaluator(null!, _expressionEvaluator, _logger);
    }
}
