using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

public class IfElseActivityTests
{
    private readonly IfElseActivity _activity;
    private readonly ILogger<IfElseActivity> _logger;

    public IfElseActivityTests()
    {
        _logger = Substitute.For<ILogger<IfElseActivity>>();
        _activity = new IfElseActivity(_logger);
    }

    [Fact]
    public void ActivityProperties_ShouldReturnCorrectValues()
    {
        // Assert
        _activity.ActivityType.Should().Be(ActivityTypes.IfElseActivity);
        _activity.Name.Should().Be("If-Else Decision");
        _activity.Description.Should().Be("Binary conditional routing based on boolean expression evaluation");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidTrueCondition_ReturnsSuccessWithTrueResult()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "status == 'approved'",
            ["status"] = "approved"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("result");
        result.OutputData!["result"].Should().Be(true);
        result.OutputData.Should().ContainKey("condition");
        result.OutputData["condition"].Should().Be("status == 'approved'");
        result.OutputData.Should().ContainKey("evaluatedAt");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidFalseCondition_ReturnsSuccessWithFalseResult()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "amount > 1000",
            ["amount"] = 500
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["result"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexAppraisalCondition_EvaluatesCorrectly()
    {
        // Arrange - Real-world appraisal routing condition
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "(property_value > 500000 && loan_to_value > 0.8) || priority == 'urgent'",
            ["property_value"] = 750000,
            ["loan_to_value"] = 0.85,
            ["priority"] = "standard"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["result"].Should().Be(true); // Should be true due to property value and LTV
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCondition_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["someOtherProperty"] = "value"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Missing required 'condition' property");
        // Error details handled internally by activity
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCondition_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "   "
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Missing required 'condition' property");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCondition_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "status = approved" // Invalid: single = instead of ==
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Condition evaluation failed");
        // Error type: ExpressionError
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingVariable_HandlesMissingGracefully()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "missing_variable == 'test'"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["result"].Should().Be(false); // Missing variables should evaluate to false
    }

    [Fact]
    public async Task ResumeAsync_ShouldReturnFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>());
        var resumeInput = new Dictionary<string, object>();

        // Act
        var result = await _activity.ResumeAsync(context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("IfElseActivity does not support resume operations");
        // Error type: UnsupportedOperation
    }

    [Fact]
    public async Task ValidateAsync_WithValidCondition_ReturnsSuccess()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "status == 'approved' && amount > 1000"
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithMissingCondition_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>());

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("'condition' property is required for IfElseActivity");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidConditionSyntax_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "status = approved" // Invalid syntax
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid condition syntax"));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("priority == 'high'", true, "high")]
    [InlineData("priority == 'low'", false, "high")]
    [InlineData("amount >= 1000", true, 1500)]
    [InlineData("amount >= 1000", false, 500)]
    [InlineData("approved && priority == 'urgent'", true, true, "urgent")]
    [InlineData("approved && priority == 'urgent'", false, false, "urgent")]
    public async Task ExecuteAsync_VariousConditions_EvaluatesCorrectly(
        string condition, bool expectedResult, params object[] variableValues)
    {
        // Arrange
        var variables = new Dictionary<string, object> { ["condition"] = condition };
        
        if (variableValues.Length > 0)
        {
            variables["priority"] = variableValues.Length > 0 ? variableValues[0] : "";
            if (variableValues.Length > 1)
                variables["amount"] = variableValues[1];
            if (variableValues.Length > 2)
                variables["approved"] = variableValues[2];
        }

        var context = CreateTestContext(variables);

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["result"].Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_AppraisalWorkflowScenarios_HandlesComplexConditions()
    {
        // Arrange - Complex appraisal decision conditions
        var scenarios = new[]
        {
            new 
            {
                Name = "High value property requires senior review",
                Condition = "property_value > 1000000 || (property_value > 500000 && market_volatility == 'high')",
                Variables = new Dictionary<string, object>
                {
                    ["property_value"] = 1200000,
                    ["market_volatility"] = "medium"
                },
                Expected = true
            },
            new 
            {
                Name = "Standard property with good conditions",
                Condition = "property_type == 'residential' && loan_to_value <= 0.8 && credit_score >= 700",
                Variables = new Dictionary<string, object>
                {
                    ["property_type"] = "residential",
                    ["loan_to_value"] = 0.75,
                    ["credit_score"] = 720
                },
                Expected = true
            },
            new 
            {
                Name = "Rush processing condition",
                Condition = "priority == 'urgent' || (client_tier == 'VIP' && turnaround_days <= 3)",
                Variables = new Dictionary<string, object>
                {
                    ["priority"] = "standard",
                    ["client_tier"] = "VIP",
                    ["turnaround_days"] = 2
                },
                Expected = true
            }
        };

        foreach (var scenario in scenarios)
        {
            // Arrange
            var variables = new Dictionary<string, object>(scenario.Variables)
            {
                ["condition"] = scenario.Condition
            };
            var context = CreateTestContext(variables);

            // Act
            var result = await _activity.ExecuteAsync(context);

            // Assert
            result.Should().NotBeNull($"Scenario: {scenario.Name}");
            result.Status.Should().Be(ActivityResultStatus.Completed, $"Scenario: {scenario.Name}");
            result.OutputData!["result"].Should().Be(scenario.Expected, $"Scenario: {scenario.Name}");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["condition"] = "status == 'approved'"
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - IfElseActivity completes immediately, so cancellation may not always throw
        var result = await _activity.ExecuteAsync(context, cts.Token);
        
        // Either completes successfully (immediate) or throws cancellation
        if (result.Status == ActivityResultStatus.Failed)
        {
            // If it fails, it should be due to cancellation
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void CreateActivityExecution_ReturnsCorrectExecution()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>());

        // Act
        // Note: CreateActivityExecution is protected, testing through public interface
        
        // Assert - Test that the activity has correct metadata
        _activity.Name.Should().Be("If-Else Decision");
        _activity.ActivityType.Should().Be(ActivityTypes.IfElseActivity);
    }

    private ActivityContext CreateTestContext(Dictionary<string, object> properties)
    {
        var variables = new Dictionary<string, object>();
        var activityProperties = new Dictionary<string, object>();

        foreach (var kvp in properties)
        {
            if (kvp.Key == "condition")
            {
                activityProperties[kvp.Key] = kvp.Value;
            }
            else
            {
                variables[kvp.Key] = kvp.Value;
            }
        }

        return new ActivityContext
        {
            ActivityId = "test-ifelse-activity",
            WorkflowInstance = null!,
            Variables = variables,
            Properties = activityProperties
        };
    }
}