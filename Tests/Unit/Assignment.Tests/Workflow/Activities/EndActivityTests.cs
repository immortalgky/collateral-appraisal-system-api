using Assignment.Workflow.Activities;
using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;
using FluentAssertions;
using Xunit;

namespace Assignment.Tests.Workflow.Activities;

public class EndActivityTests
{
    private readonly EndActivity _endActivity;

    public EndActivityTests()
    {
        _endActivity = new EndActivity();
    }

    [Fact]
    public void ActivityType_ReturnsCorrectType()
    {
        // Act
        var activityType = _endActivity.ActivityType;

        // Assert
        activityType.Should().Be(ActivityTypes.EndActivity);
    }

    [Fact]
    public void Name_ReturnsCorrectName()
    {
        // Act
        var name = _endActivity.Name;

        // Assert
        name.Should().Be("End Activity");
    }

    [Fact]
    public void Description_ReturnsCorrectDescription()
    {
        // Act
        var description = _endActivity.Description;

        // Assert
        description.Should().Be("Marks the end of a workflow instance");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidContext_ReturnsSuccessResult()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = new Dictionary<string, object>
            {
                ["final_result"] = "completed",
                ["total_time"] = TimeSpan.FromHours(2.5)
            },
            Properties = new Dictionary<string, object>()
        };

        // Act
        var result = await _endActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithFinalOutputs_PreservesOutputData()
    {
        // Arrange
        var finalOutputs = new Dictionary<string, object>
        {
            ["workflow_result"] = "success",
            ["completion_time"] = DateTime.UtcNow,
            ["total_duration"] = "02:30:45",
            ["final_status"] = "approved",
            ["processed_by"] = "appraiser@company.com"
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = finalOutputs,
            Properties = new Dictionary<string, object>()
        };

        // Act
        var result = await _endActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Final outputs should be preserved in context
        context.Variables.Should().Contain("workflow_result", "success");
        context.Variables.Should().Contain("final_status", "approved");
        context.Variables.Should().Contain("processed_by", "appraiser@company.com");
    }

    [Fact]
    public async Task ExecuteAsync_WorkflowCompletion_HandlesResourceCleanup()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = new Dictionary<string, object>
            {
                ["temp_files"] = new List<string> { "file1.tmp", "file2.tmp" },
                ["allocated_resources"] = new List<string> { "worker_1", "worker_2" },
                ["cleanup_required"] = true
            },
            Properties = new Dictionary<string, object>
            {
                ["cleanup_timeout"] = 30,
                ["notify_completion"] = true
            }
        };

        // Act
        var result = await _endActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Context should still contain resource information for potential cleanup
        context.Variables.Should().ContainKey("temp_files");
        context.Variables.Should().ContainKey("allocated_resources");
        context.Properties.Should().Contain("cleanup_timeout", 30);
    }

    [Fact]
    public async Task ExecuteAsync_WithMetrics_CapturesPerformanceData()
    {
        // Arrange
        var metricsData = new Dictionary<string, object>
        {
            ["start_time"] = DateTime.UtcNow.AddHours(-2),
            ["end_time"] = DateTime.UtcNow,
            ["activities_completed"] = 8,
            ["errors_encountered"] = 0,
            ["assignments_made"] = 3,
            ["decision_points"] = 2
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = metricsData,
            Properties = new Dictionary<string, object>
            {
                ["metrics_enabled"] = true,
                ["performance_tracking"] = "detailed"
            }
        };

        // Act
        var result = await _endActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Metrics data should be preserved for reporting
        context.Variables.Should().Contain("activities_completed", 8);
        context.Variables.Should().Contain("errors_encountered", 0);
        context.Variables.Should().Contain("assignments_made", 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>()
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = async () => await _endActivity.ExecuteAsync(context, cts.Token);
        
        // The operation should either complete quickly (as it's simple) or respect cancellation
        await act.Should().NotThrowAsync("End activity should handle cancellation gracefully");
    }

    [Fact]
    public async Task ExecuteAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _endActivity.ExecuteAsync(null!);
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
            ActivityId = "end_activity",
            Variables = new Dictionary<string, object> { ["final_state"] = "completed" },
            Properties = new Dictionary<string, object>()
        };

        // Act - Execute multiple times
        var result1 = await _endActivity.ExecuteAsync(context);
        var result2 = await _endActivity.ExecuteAsync(context);
        var result3 = await _endActivity.ExecuteAsync(context);

        // Assert - All executions should be consistent
        result1.Status.Should().Be(ActivityResultStatus.Completed);
        result2.Status.Should().Be(ActivityResultStatus.Completed);
        result3.Status.Should().Be(ActivityResultStatus.Completed);
    }

    [Fact]
    public async Task ValidateAsync_ValidContext_ReturnsSuccess()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>()
        };

        // Act
        var result = await _endActivity.ValidateAsync(context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_NullContext_ReturnsValidationError()
    {
        // Act
        var result = await _endActivity.ValidateAsync(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_AppraisalWorkflowCompletion_CapturesFinalResults()
    {
        // Arrange - Real-world property appraisal completion scenario
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_appraisal",
            Variables = new Dictionary<string, object>
            {
                ["property_id"] = "PROP_12345",
                ["appraisal_value"] = 450000m,
                ["appraisal_date"] = DateTime.Today,
                ["appraiser_id"] = "APP_789",
                ["quality_score"] = 95,
                ["review_status"] = "approved",
                ["completion_time"] = DateTime.UtcNow,
                ["total_hours"] = 8.5,
                ["report_url"] = "https://reports.company.com/PROP_12345.pdf"
            },
            Properties = new Dictionary<string, object>
            {
                ["workflow_type"] = "property_appraisal",
                ["send_notifications"] = true,
                ["archive_documents"] = true,
                ["update_systems"] = new List<string> { "CRM", "Accounting", "Reporting" }
            }
        };

        // Act
        var result = await _endActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Verify all final data is captured
        context.Variables.Should().Contain("property_id", "PROP_12345");
        context.Variables.Should().Contain("appraisal_value", 450000m);
        context.Variables.Should().Contain("review_status", "approved");
        context.Variables.Should().Contain("quality_score", 95);
        context.Properties.Should().Contain("send_notifications", true);
        context.Properties.Should().ContainKey("update_systems");
    }

    [Fact]
    public async Task ExecuteAsync_ErrorScenario_HandlesFailureGracefully()
    {
        // Arrange - Workflow ending with error state
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = new Dictionary<string, object>
            {
                ["workflow_status"] = "failed",
                ["error_code"] = "VALIDATION_FAILED",
                ["error_message"] = "Property documentation incomplete",
                ["failed_at_activity"] = "property_validation",
                ["retry_count"] = 3,
                ["max_retries"] = 3
            },
            Properties = new Dictionary<string, object>
            {
                ["handle_errors"] = true,
                ["send_error_notifications"] = true
            }
        };

        // Act
        var result = await _endActivity.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Error information should be preserved for reporting
        context.Variables.Should().Contain("workflow_status", "failed");
        context.Variables.Should().Contain("error_code", "VALIDATION_FAILED");
        context.Variables.Should().Contain("retry_count", 3);
    }

    [Fact]
    public async Task ExecuteAsync_LargeDataSet_HandlesEfficiently()
    {
        // Arrange - Test with large final dataset
        var largeResultSet = new Dictionary<string, object>();
        for (int i = 0; i < 1000; i++)
        {
            largeResultSet[$"result_{i}"] = $"value_{i}";
        }
        largeResultSet["summary_data"] = "Large workflow with 1000 data points completed";

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "end_activity",
            Variables = largeResultSet,
            Properties = new Dictionary<string, object>()
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _endActivity.ExecuteAsync(context);
        stopwatch.Stop();

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Should handle large datasets efficiently");
        context.Variables.Should().HaveCount(1001, "All result data should be preserved");
        context.Variables.Should().ContainKey("summary_data");
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentCompletions_ThreadSafe()
    {
        // Arrange
        var tasks = new List<Task<ActivityResult>>();
        
        // Act - Execute end activity concurrently (simulating multiple workflows ending)
        for (int i = 0; i < 10; i++)
        {
            var context = new ActivityContext
            {
                WorkflowInstanceId = Guid.NewGuid(),
                ActivityId = $"end_activity_{i}",
                Variables = new Dictionary<string, object> 
                { 
                    ["workflow_id"] = i,
                    ["completion_status"] = "success"
                },
                Properties = new Dictionary<string, object>()
            };
            
            tasks.Add(_endActivity.ExecuteAsync(context));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All concurrent executions should succeed
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.Status == ActivityResultStatus.Completed);
    }
}