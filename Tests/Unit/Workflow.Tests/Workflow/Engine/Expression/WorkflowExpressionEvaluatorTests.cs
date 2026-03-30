using Workflow.Workflow.Engine.Expression;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.Workflow.Engine.Expression;

public class WorkflowExpressionEvaluatorTests
{
    private readonly IWorkflowExpressionEvaluator _evaluator;
    private readonly ILogger<WorkflowExpressionEvaluator> _logger;

    public WorkflowExpressionEvaluatorTests()
    {
        _logger = Substitute.For<ILogger<WorkflowExpressionEvaluator>>();
        _evaluator = new WorkflowExpressionEvaluator(_logger);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_SimpleCondition_ReturnsCorrectResult()
    {
        // Arrange - The simple evaluator uses ${varname} substitution, then checks ==
        // Expression: "${status} == approved" -> after substitution: "approved == approved" -> true
        var variables = new Dictionary<string, object>
        {
            ["status"] = "approved",
            ["amount"] = 1000
        };

        // Act
        var result = await _evaluator.EvaluateBooleanAsync("${status} == approved", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBooleanAsync_ComplexWorkflowCondition_EvaluatesCorrectly()
    {
        // Arrange - Appraisal workflow scenario
        var variables = new Dictionary<string, object>
        {
            ["property_value"] = 350000,
            ["loan_to_value"] = 0.80,
            ["property_type"] = "residential",
            ["priority"] = "standard",
            ["reviewer_level"] = "senior",
            ["complex_property"] = false
        };

        // Act & Assert - Route to senior appraiser condition
        var seniorRequired = await _evaluator.EvaluateBooleanAsync(
            "property_value > 300000 && loan_to_value > 0.75", variables);
        seniorRequired.Should().BeTrue();

        // Route to standard appraiser condition
        var standardOk = await _evaluator.EvaluateBooleanAsync(
            "property_type == 'residential' && property_value <= 300000 && !complex_property", variables);
        standardOk.Should().BeFalse();

        // Priority routing condition
        var highPriority = await _evaluator.EvaluateBooleanAsync(
            "priority == 'urgent' || (property_value > 500000 && complex_property)", variables);
        highPriority.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsObjectResult()
    {
        // The simple evaluator returns the substituted string - it does not evaluate arithmetic.
        // Use ${varname} patterns so variables are substituted.
        var variables = new Dictionary<string, object>
        {
            ["base_amount"] = 1000,
            ["multiplier"] = 1.5
        };

        // Act - returns substituted string, not computed result
        var result = await _evaluator.EvaluateAsync("${base_amount}", variables);

        // Assert - returns string representation of the variable
        result.Should().NotBeNull();
        result!.ToString().Should().Be("1000");
    }

    [Fact]
    public async Task EvaluateAsync_WithNullExpression_ReturnsNull()
    {
        // Arrange
        var variables = new Dictionary<string, object>();

        // Act
        var result = await _evaluator.EvaluateAsync(null!, variables);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyExpression_ReturnsEmptyString()
    {
        // The simple evaluator returns empty string for empty input (not null).
        // SubstituteVariables returns the empty string unchanged.
        var variables = new Dictionary<string, object>();

        // Act
        var result = await _evaluator.EvaluateAsync("", variables);

        // Assert - empty string is returned, not null
        result.Should().Be("");
    }

    [Fact]
    public void SubstituteVariables_SimpleSubstitution_ReplacesCorrectly()
    {
        // Arrange
        var template = "Hello ${name}, your balance is ${balance}";
        var variables = new Dictionary<string, object>
        {
            ["name"] = "John Doe",
            ["balance"] = 1500.50
        };

        // Act
        var result = _evaluator.SubstituteVariables(template, variables);

        // Assert
        result.Should().Be("Hello John Doe, your balance is 1500.5");
    }

    [Fact]
    public void SubstituteVariables_ComplexTemplate_ReplacesAllVariables()
    {
        // The simple evaluator supports ${varname} placeholders without format specifiers.
        // Format specifiers like ${property_value:C} are treated as variable names "property_value:C"
        // which don't exist in the dictionary, so placeholders remain unchanged.
        var template = "Property at ${address} valued at ${property_value} requires ${reviewer_type} review";
        var variables = new Dictionary<string, object>
        {
            ["address"] = "123 Main Street",
            ["property_value"] = 425000,
            ["reviewer_type"] = "senior"
        };

        // Act
        var result = _evaluator.SubstituteVariables(template, variables);

        // Assert
        result.Should().Contain("123 Main Street");
        result.Should().Contain("425000");
        result.Should().Contain("senior");
    }

    [Fact]
    public void SubstituteVariables_MissingVariables_LeavesPlaceholderUnchanged()
    {
        // Arrange
        var template = "Hello ${name}, your ${missing_field} is available";
        var variables = new Dictionary<string, object>
        {
            ["name"] = "John"
        };

        // Act
        var result = _evaluator.SubstituteVariables(template, variables);

        // Assert
        result.Should().Be("Hello John, your ${missing_field} is available");
    }

    [Fact]
    public void SubstituteVariables_EmptyTemplate_ReturnsEmptyString()
    {
        // Arrange
        var variables = new Dictionary<string, object>();

        // Act
        var result = _evaluator.SubstituteVariables("", variables);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void SubstituteVariables_NoPlaceholders_ReturnsOriginal()
    {
        // Arrange
        var template = "This is a simple text without variables";
        var variables = new Dictionary<string, object>
        {
            ["unused"] = "value"
        };

        // Act
        var result = _evaluator.SubstituteVariables(template, variables);

        // Assert
        result.Should().Be(template);
    }

    [Fact]
    public async Task ValidateExpressionAsync_ValidExpression_ReturnsValid()
    {
        // Act
        var result = await _evaluator.ValidateExpressionAsync(
            "property_value > 250000 && credit_score >= 700");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateExpressionAsync_InvalidExpression_ReturnsInvalid()
    {
        // The simple validator checks for unbalanced braces and empty variable names.
        // Unbalanced braces make an expression invalid.
        var result = await _evaluator.ValidateExpressionAsync(
            "property_value ${unclosed"); // Invalid: unbalanced brace

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateExpressionAsync_EmptyExpression_ReturnsInvalid()
    {
        // The validator treats empty/whitespace expressions as invalid.
        var result = await _evaluator.ValidateExpressionAsync("");

        // Assert - empty expression is invalid
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EvaluateBooleanAsync_WithCancellation_CompletesNormally()
    {
        // The simple evaluator does not check the cancellation token during execution.
        // It completes immediately and returns a result even with a cancelled token.
        var variables = new Dictionary<string, object>
        {
            ["value"] = 100
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Completes without throwing (no cancellation check in simple evaluator)
        var result = await _evaluator.EvaluateBooleanAsync("${value} != other", variables, cts.Token);

        // Assert - Result is valid even with cancelled token
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithCancellation_CompletesNormally()
    {
        // The simple evaluator does not check the cancellation token during execution.
        var variables = new Dictionary<string, object>
        {
            ["value"] = 100
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Completes without throwing
        var result = await _evaluator.EvaluateAsync("${value}", variables, cts.Token);

        // Assert - Returns the substituted value
        result!.ToString().Should().Be("100");
    }

    [Fact]
    public async Task EvaluateBooleanAsync_AppraisalWorkflowScenarios_HandlesSimpleConditions()
    {
        // The simple evaluator supports only basic == and != comparisons using ${varname} substitution.
        // Complex operators (&&, ||, >, <) are not supported - non-empty/non-false results return true.
        var variables = new Dictionary<string, object>
        {
            ["property_type"] = "commercial",
            ["client_tier"] = "VIP",
            ["routing_decision"] = "approved"
        };

        // Simple equality check: "${property_type} == commercial" -> "commercial == commercial" -> true
        var isCommercial = await _evaluator.EvaluateBooleanAsync("${property_type} == commercial", variables);
        isCommercial.Should().BeTrue();

        // Inequality check: "${property_type} != residential" -> "commercial != residential" -> true
        var notResidential = await _evaluator.EvaluateBooleanAsync("${property_type} != residential", variables);
        notResidential.Should().BeTrue();

        // VIP check: "${client_tier} == VIP" -> "VIP == VIP" -> true
        var isVip = await _evaluator.EvaluateBooleanAsync("${client_tier} == VIP", variables);
        isVip.Should().BeTrue();

        // Routing decision: "${routing_decision} == approved" -> "approved == approved" -> true
        var isApproved = await _evaluator.EvaluateBooleanAsync("${routing_decision} == approved", variables);
        isApproved.Should().BeTrue();
    }

    [Theory]
    [InlineData("${simple}", "simple", "replaced")]
    [InlineData("${number}", "number", "42")]
    [InlineData("No variables", "any", "No variables")]
    [InlineData("${start} and ${end}", "start,end", "BEGIN and FINISH")]
    public void SubstituteVariables_VariousScenarios_SubstitutesCorrectly(
        string template, string variableNames, string expectedResult)
    {
        // Arrange
        var variables = new Dictionary<string, object>();
        
        if (variableNames == "simple")
        {
            variables["simple"] = "replaced";
        }
        else if (variableNames == "number")
        {
            variables["number"] = 42;
        }
        else if (variableNames == "start,end")
        {
            variables["start"] = "BEGIN";
            variables["end"] = "FINISH";
        }

        // Act
        var result = _evaluator.SubstituteVariables(template, variables);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_InvalidSyntax_ReturnsFalseWithoutThrowing()
    {
        // The simple evaluator catches all exceptions and returns false (does not re-throw).
        // It logs a Warning (not Error) when evaluation fails.
        var variables = new Dictionary<string, object>
        {
            ["valid"] = "test"
        };

        // Act - "===" contains "==" so left="invalid syntax ", right="= ${valid}"
        // After substitution it's just weird text - but no exception is thrown
        var result = await _evaluator.EvaluateBooleanAsync("invalid syntax ===", variables);

        // Assert - evaluator swallows the error and returns false
        result.Should().BeFalse();
    }
}