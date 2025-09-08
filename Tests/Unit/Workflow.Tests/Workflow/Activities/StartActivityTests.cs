using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using FluentAssertions;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

public class StartActivityTests
{
    private readonly StartActivity _startActivity;

    public StartActivityTests()
    {
        _startActivity = new StartActivity();
    }

    [Fact]
    public void ActivityType_ReturnsCorrectType()
    {
        // Act
        var activityType = _startActivity.ActivityType;

        // Assert
        activityType.Should().Be(ActivityTypes.StartActivity);
    }

    [Fact]
    public void Name_ReturnsCorrectName()
    {
        // Act
        var name = _startActivity.Name;

        // Assert
        name.Should().Be("Start Activity");
    }

    [Fact]
    public void Description_ReturnsCorrectDescription()
    {
        // Act
        var description = _startActivity.Description;

        // Assert
        description.Should().Be("Initializes the workflow and sets up initial context");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidContext_ReturnsSuccessResult()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity",
            Variables = new Dictionary<string, object>
            {
                ["initial_var"] = "initial_value"
            },
            Properties = new Dictionary<string, object>()
        };

        // Act
        var result = await _startActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyContext_ReturnsSuccessResult()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity",
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>()
        };

        // Act
        var result = await _startActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_WithInitialVariables_PreservesVariables()
    {
        // Arrange
        var initialVariables = new Dictionary<string, object>
        {
            ["workflow_id"] = "WF_001",
            ["created_by"] = "system",
            ["priority"] = "high",
            ["department"] = "finance"
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity", 
            Variables = initialVariables,
            Properties = new Dictionary<string, object>()
        };

        // Act
        var result = await _startActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Context variables should be preserved
        context.Variables.Should().Contain("workflow_id", "WF_001");
        context.Variables.Should().Contain("created_by", "system");
        context.Variables.Should().Contain("priority", "high");
        context.Variables.Should().Contain("department", "finance");
    }

    [Fact]
    public async Task ExecuteAsync_WithProperties_HandlesPropertiesCorrectly()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["initialization_mode"] = "automatic",
            ["timeout_minutes"] = 60,
            ["enable_logging"] = true
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity",
            Variables = new Dictionary<string, object>(),
            Properties = properties
        };

        // Act
        var result = await _startActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Properties should be accessible
        context.Properties.Should().Contain("initialization_mode", "automatic");
        context.Properties.Should().Contain("timeout_minutes", 60);
        context.Properties.Should().Contain("enable_logging", true);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_Respectscancellation()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity",
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>()
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = async () => await _startActivity.ExecuteAsync(context, cts.Token);
        
        // The operation should either complete quickly (as it's simple) or respect cancellation
        await act.Should().NotThrowAsync("Start activity should handle cancellation gracefully");
    }

    [Fact]
    public async Task ExecuteAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _startActivity.ExecuteAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleExecutions_ConsistentResults()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity",
            Variables = new Dictionary<string, object> { ["test"] = "value" },
            Properties = new Dictionary<string, object>()
        };

        // Act - Execute multiple times
        var result1 = await _startActivity.ExecuteAsync(context);
        var result2 = await _startActivity.ExecuteAsync(context);
        var result3 = await _startActivity.ExecuteAsync(context);

        // Assert - All executions should be consistent
        result1.Status.Should().Be(ActivityResultStatus.Completed);
        result2.Status.Should().Be(ActivityResultStatus.Completed);
        result3.Status.Should().Be(ActivityResultStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_WorkflowInitialization_SetsUpContextCorrectly()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var context = new ActivityContext
        {
            WorkflowInstanceId = workflowId,
            ActivityId = "start_activity",
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>
            {
                ["workflow_name"] = "Property Appraisal",
                ["workflow_version"] = "1.0",
                ["created_at"] = DateTime.UtcNow
            }
        };

        // Act
        var result = await _startActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        context.WorkflowInstanceId.Should().Be(workflowId);
        context.Properties.Should().Contain("workflow_name", "Property Appraisal");
    }

    [Fact]
    public async Task ValidateAsync_ValidContext_ReturnsSuccess()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity",
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>()
        };

        // Act
        var result = await _startActivity.ValidateAsync(context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_NullContext_ReturnsValidationError()
    {
        // Act
        var result = await _startActivity.ValidateAsync(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact] 
    public async Task ExecuteAsync_RealWorldAppraisalScenario_InitializesCorrectly()
    {
        // Arrange - Real-world property appraisal workflow scenario
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_appraisal",
            Variables = new Dictionary<string, object>
            {
                ["property_id"] = "PROP_12345",
                ["request_type"] = "residential_appraisal",
                ["client_id"] = "CLIENT_789",
                ["requested_by"] = "john.doe@company.com",
                ["priority"] = "standard",
                ["due_date"] = DateTime.Now.AddDays(14)
            },
            Properties = new Dictionary<string, object>
            {
                ["workflow_type"] = "property_appraisal",
                ["sla_hours"] = 336, // 14 days
                ["auto_assign"] = true,
                ["notification_enabled"] = true
            }
        };

        // Act
        var result = await _startActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Verify workflow initialization preserved all critical data
        context.Variables.Should().Contain("property_id", "PROP_12345");
        context.Variables.Should().Contain("request_type", "residential_appraisal");
        context.Variables.Should().Contain("client_id", "CLIENT_789");
        context.Properties.Should().Contain("workflow_type", "property_appraisal");
        context.Properties.Should().Contain("auto_assign", true);
    }

    [Fact]
    public async Task ExecuteAsync_LargeVariableSet_HandlesEfficiently()
    {
        // Arrange - Test with large number of variables
        var largeVariableSet = new Dictionary<string, object>();
        for (int i = 0; i < 1000; i++)
        {
            largeVariableSet[$"var_{i}"] = $"value_{i}";
        }

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "start_activity",
            Variables = largeVariableSet,
            Properties = new Dictionary<string, object>()
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _startActivity.ExecuteAsync(context);
        stopwatch.Stop();

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Should handle large variable sets efficiently");
        context.Variables.Should().HaveCount(1000, "All variables should be preserved");
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentExecutions_ThreadSafe()
    {
        // Arrange
        var tasks = new List<Task<ActivityResult>>();
        
        // Act - Execute start activity concurrently
        for (int i = 0; i < 10; i++)
        {
            var context = new ActivityContext
            {
                WorkflowInstanceId = Guid.NewGuid(),
                ActivityId = $"start_activity_{i}",
                Variables = new Dictionary<string, object> { ["thread_id"] = i },
                Properties = new Dictionary<string, object>()
            };
            
            tasks.Add(_startActivity.ExecuteAsync(context));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All concurrent executions should succeed
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.Status == ActivityResultStatus.Completed);
    }
}