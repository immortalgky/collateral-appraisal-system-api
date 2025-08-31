using Assignment.Workflow.Engine.Expression;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Assignment.Tests.Workflow.Engine.Expression;

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
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["status"] = "approved",
            ["amount"] = 1000
        };

        // Act
        var result = await _evaluator.EvaluateBooleanAsync("status == 'approved'", variables);

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
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["base_amount"] = 1000,
            ["multiplier"] = 1.5
        };

        // Act
        var result = await _evaluator.EvaluateAsync("base_amount * multiplier", variables);

        // Assert
        result.Should().NotBeNull();
        Convert.ToDouble(result).Should().Be(1500.0);
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
    public async Task EvaluateAsync_WithEmptyExpression_ReturnsNull()
    {
        // Arrange
        var variables = new Dictionary<string, object>();

        // Act
        var result = await _evaluator.EvaluateAsync("", variables);

        // Assert
        result.Should().BeNull();
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
        // Arrange
        var template = "Property at ${address} valued at ${property_value:C} requires ${reviewer_type} review";
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
        // Act
        var result = await _evaluator.ValidateExpressionAsync(
            "property_value = 250000"); // Invalid: single = instead of ==

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateExpressionAsync_EmptyExpression_ReturnsValid()
    {
        // Act
        var result = await _evaluator.ValidateExpressionAsync("");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateBooleanAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["value"] = 100
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _evaluator.EvaluateBooleanAsync("value > 50", variables, cts.Token));
    }

    [Fact]
    public async Task EvaluateAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["value"] = 100
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _evaluator.EvaluateAsync("value * 2", variables, cts.Token));
    }

    [Fact]
    public async Task EvaluateBooleanAsync_AppraisalWorkflowScenarios_HandlesRealConditions()
    {
        // Arrange - Complex appraisal routing scenarios
        var variables = new Dictionary<string, object>
        {
            ["property_value"] = 750000,
            ["loan_amount"] = 600000,
            ["property_type"] = "commercial",
            ["complexity_score"] = 8.5,
            ["client_tier"] = "VIP",
            ["rush_request"] = false,
            ["previous_appraisal_age"] = 180, // days
            ["market_volatility"] = "high"
        };

        // Act & Assert - Complex routing conditions
        
        // Senior appraiser required
        var seniorRequired = await _evaluator.EvaluateBooleanAsync(
            "(property_value > 500000 && property_type == 'commercial') || complexity_score > 7", variables);
        seniorRequired.Should().BeTrue();

        // Committee review required
        var committeeRequired = await _evaluator.EvaluateBooleanAsync(
            "property_value > 1000000 || (client_tier == 'VIP' && complexity_score > 9)", variables);
        committeeRequired.Should().BeFalse();

        // Rush processing
        var rushProcessing = await _evaluator.EvaluateBooleanAsync(
            "rush_request || (client_tier == 'VIP' && property_value > 500000)", variables);
        rushProcessing.Should().BeTrue();

        // Updated appraisal needed
        var updateRequired = await _evaluator.EvaluateBooleanAsync(
            "previous_appraisal_age > 90 && market_volatility == 'high'", variables);
        updateRequired.Should().BeTrue();

        // Automated approval eligible
        var autoApprovalEligible = await _evaluator.EvaluateBooleanAsync(
            "property_type == 'residential' && property_value <= 400000 && complexity_score <= 5", variables);
        autoApprovalEligible.Should().BeFalse();
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
    public async Task EvaluateBooleanAsync_ErrorHandling_LogsAndThrows()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["valid"] = "test"
        };

        // Act & Assert
        var act = async () => await _evaluator.EvaluateBooleanAsync("invalid syntax ===", variables);
        await act.Should().ThrowAsync<Exception>();

        // Verify logging occurred
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("Error evaluating")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}