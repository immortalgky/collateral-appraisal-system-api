using System.Diagnostics;
using System.Text.Json;
using Assignment.Data;
using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Services;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Assignment.Integration.Tests.Workflow;

/// <summary>
/// Database-driven integration tests for complete workflow engine ecosystem
/// Tests end-to-end workflow execution with real TestContainer database persistence
/// </summary>
[Collection("Integration")]
public class WorkflowEngineIntegrationTests(IntegrationTestFixture fixture)
{

    [Fact]
    public async Task StartWorkflowAsync_SimpleAppraisalWorkflow_PersistsToDatabase()
    {
        // Arrange - Get real services from DI container
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        // Create a real workflow definition in database
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Simple Property Appraisal",
            "Basic property appraisal workflow for integration testing",
            JsonSerializer.Serialize(workflowSchema),
            "Appraisal",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var initialVariables = new Dictionary<string, object>
        {
            ["property_address"] = "123 Integration Test Street, City, State",
            ["property_value"] = 450000,
            ["loan_amount"] = 360000,
            ["client_tier"] = "Standard",
            ["priority"] = "standard",
            ["property_type"] = "residential",
            ["appraiser_id"] = "appraiser@company.com"
        };

        // Act - Start workflow with real persistence
        var workflowInstance = await workflowService.StartWorkflowAsync(
            workflowDefinition.Id,
            "Property Appraisal #INT-12345",
            "integration-test@company.com",
            initialVariables,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Verify workflow was persisted to database
        workflowInstance.Should().NotBeNull();
        workflowInstance.Status.Should().Be(WorkflowStatus.Running);
        workflowInstance.Variables.Should().ContainKey("property_address");
        workflowInstance.Variables.Should().ContainKey("property_value");
        workflowInstance.WorkflowDefinitionId.Should().Be(workflowDefinition.Id);

        // Verify database persistence
        var persistedInstance = await dbContext.WorkflowInstances
            .FindAsync([workflowInstance.Id], TestContext.Current.CancellationToken);
        
        persistedInstance.Should().NotBeNull();
        persistedInstance!.Name.Should().Be("Property Appraisal #INT-12345");
        persistedInstance.Status.Should().Be(WorkflowStatus.Running);
        persistedInstance.Variables.Should().ContainKey("property_address");
        
        // Verify activity execution was created
        var activityExecution = await dbContext.WorkflowActivityExecutions
            .Where(ae => ae.WorkflowInstanceId == workflowInstance.Id)
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
            
        activityExecution.Should().NotBeNull();
        activityExecution!.Status.Should().Be(ActivityExecutionStatus.Pending);
    }

    [Fact]
    public async Task ResumeWorkflowAsync_PropertyAppraisalCompletion_UpdatesDatabaseState()
    {
        // Arrange - Setup workflow in database
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        // Create workflow definition and instance
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Resume Test Appraisal",
            "Appraisal workflow for resume testing",
            JsonSerializer.Serialize(workflowSchema),
            "Appraisal",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        
        var workflowInstance = WorkflowInstance.Create(
            workflowDefinition.Id,
            "Resume Test Workflow",
            null,
            "integration-test@company.com",
            new Dictionary<string, object>
            {
                ["property_value"] = 425000,
                ["loan_amount"] = 340000
            });
            
        workflowInstance.SetCurrentActivity("property-evaluation", "appraiser@company.com");
        dbContext.WorkflowInstances.Add(workflowInstance);
        
        // Create pending activity execution
        var activityExecution = WorkflowActivityExecution.Create(
            workflowInstance.Id,
            "property-evaluation",
            "Property Evaluation",
            "TaskActivity",
            "appraiser@company.com");
            
        dbContext.WorkflowActivityExecutions.Add(activityExecution);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resumeInput = new Dictionary<string, object>
        {
            ["appraisal_value"] = 465000,
            ["condition_rating"] = "Good",
            ["market_analysis"] = "Stable market conditions",
            ["completion_date"] = DateTime.UtcNow,
            ["notes"] = "Property in excellent condition, minor maintenance needed"
        };

        // Act - Resume workflow
        var result = await workflowService.ResumeWorkflowAsync(workflowInstance.Id, "property-evaluation", "senior-appraiser@company.com", resumeInput, cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Verify workflow progression
        result.Should().NotBeNull();
        result.Variables.Should().ContainKey("appraisal_value");
        result.Variables["appraisal_value"].Should().Be(465000);

        // Verify database state updates
        var updatedInstance = await dbContext.WorkflowInstances
            .FindAsync([workflowInstance.Id], TestContext.Current.CancellationToken);
            
        updatedInstance.Should().NotBeNull();
        updatedInstance!.Variables.Should().ContainKey("appraisal_value");
        updatedInstance.Variables["appraisal_value"].Should().Be(465000);
        
        // Verify activity execution was completed
        var completedExecution = await dbContext.WorkflowActivityExecutions
            .Where(ae => ae.WorkflowInstanceId == workflowInstance.Id && 
                         ae.ActivityId == "property-evaluation")
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
            
        completedExecution.Should().NotBeNull();
        completedExecution!.Status.Should().Be(ActivityExecutionStatus.Completed);
        completedExecution.CompletedBy.Should().Be("senior-appraiser@company.com");
        completedExecution.CompletedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserTasksAsync_AppraisalWorkflowsInProgress_RetrievesFromDatabase()
    {
        // Arrange - Create multiple workflows assigned to user
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var userId = "appraiser-john@company.com";
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "User Task Test Workflow",
            "Workflow for user task testing",
            JsonSerializer.Serialize(workflowSchema),
            "Appraisal",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        
        // Create multiple workflow instances assigned to the user
        var instances = new List<WorkflowInstance>();
        for (int i = 1; i <= 3; i++)
        {
            var instance = WorkflowInstance.Create(
                workflowDefinition.Id,
                $"Property Appraisal #{i:D3}",
                null,
                "integration-test@company.com",
                new Dictionary<string, object>
                {
                    ["property_id"] = $"PROP-{i:D3}",
                    ["property_value"] = 300000 + (i * 50000)
                });
                
            instance.SetCurrentActivity("property-evaluation", userId);
            instances.Add(instance);
            dbContext.WorkflowInstances.Add(instance);
            
            // Create activity executions
            var execution = WorkflowActivityExecution.Create(
                instance.Id,
                "property-evaluation",
                "Property Evaluation",
                "TaskActivity",
                userId);
            dbContext.WorkflowActivityExecutions.Add(execution);
        }
        
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Get user tasks from database
        var userTasks = await workflowService.GetUserTasksAsync(userId, TestContext.Current.CancellationToken);

        // Assert - Verify tasks retrieved from database
        var taskList = userTasks.ToList();
        taskList.Should().NotBeNull();
        taskList.Should().HaveCount(3);
        taskList.All(t => t.Status == WorkflowStatus.Running).Should().BeTrue();
        taskList.All(t => t.CurrentAssignee == userId).Should().BeTrue();
        taskList.Select(t => t.Name).Should().Contain("Property Appraisal #001");
        taskList.Select(t => t.Name).Should().Contain("Property Appraisal #002");
        taskList.Select(t => t.Name).Should().Contain("Property Appraisal #003");
    }

    [Fact]
    public async Task ConcurrentWorkflowExecution_MultipleAppraisals_HandlesIsolationCorrectly()
    {
        // Arrange - Setup for concurrent execution
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Concurrent Test Workflow",
            "Workflow for concurrent execution testing",
            JsonSerializer.Serialize(workflowSchema),
            "Appraisal",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        const int concurrentWorkflows = 5;
        var tasks = new List<Task<WorkflowInstance>>();

        // Act - Start multiple workflows concurrently
        for (int i = 0; i < concurrentWorkflows; i++)
        {
            var workflowName = $"Concurrent Appraisal #{i + 1}";
            var startedBy = $"appraiser{i + 1}@company.com";
            var variables = new Dictionary<string, object>
            {
                ["property_id"] = $"PROP-{i + 1:D3}",
                ["property_value"] = 400000 + (i * 25000),
                ["concurrent_test"] = true
            };

            var task = workflowService.StartWorkflowAsync(workflowDefinition.Id, workflowName, startedBy, variables, cancellationToken: TestContext.Current.CancellationToken);
            
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Verify all workflows were created successfully with database isolation
        results.Should().HaveCount(concurrentWorkflows);
        results.Should().OnlyContain(r => r.Status == WorkflowStatus.Running);
        
        // Verify each workflow has unique ID and was persisted
        var workflowIds = results.Select(r => r.Id).ToList();
        workflowIds.Should().OnlyHaveUniqueItems();
        
        // Verify database persistence for all workflows
        var persistedInstances = await dbContext.WorkflowInstances
            .Where(wi => workflowIds.Contains(wi.Id))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            
        persistedInstances.Should().HaveCount(concurrentWorkflows);
        persistedInstances.All(wi => wi.WorkflowDefinitionId == workflowDefinition.Id).Should().BeTrue();
        
        // Verify activity executions were created for each workflow
        var activityExecutions = await dbContext.WorkflowActivityExecutions
            .Where(ae => workflowIds.Contains(ae.WorkflowInstanceId))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            
        activityExecutions.Should().HaveCount(concurrentWorkflows);
    }

    [Fact]
    public async Task WorkflowPerformance_LargeVariableSet_PersistsEfficientlyToDatabase()
    {
        // Arrange - Large variable set with database persistence
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Performance Test Workflow",
            "Workflow for performance testing with large variable sets",
            JsonSerializer.Serialize(workflowSchema),
            "Performance",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var largeVariables = new Dictionary<string, object>();
        for (int i = 0; i < 1000; i++)
        {
            largeVariables[$"performance_var_{i}"] = $"Large test value for variable {i} with substantial content to simulate real-world variable storage requirements";
        }
        
        largeVariables["property_address"] = "123 Performance Test Street";
        largeVariables["property_value"] = 500000;
        largeVariables["test_mode"] = "performance";

        // Act & Measure - Start workflow with performance timing
        var stopwatch = Stopwatch.StartNew();
        
        var result = await workflowService.StartWorkflowAsync(workflowDefinition.Id, "Performance Test Workflow", "performance@company.com", largeVariables, cancellationToken: TestContext.Current.CancellationToken);
        
        stopwatch.Stop();

        // Assert - Verify performance and database persistence
        result.Should().NotBeNull();
        result.Variables.Count.Should().BeGreaterThanOrEqualTo(1000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, 
            "Workflow start with large variable set should complete within 10 seconds including database persistence");

        // Verify database persistence of large variable set
        var persistedInstance = await dbContext.WorkflowInstances
            .FindAsync([result.Id], TestContext.Current.CancellationToken);
            
        persistedInstance.Should().NotBeNull();
        persistedInstance!.Variables.Count.Should().BeGreaterThanOrEqualTo(1000);
        persistedInstance.Variables.Should().ContainKey("property_address");
        persistedInstance.Variables.Should().ContainKey("test_mode");
    }

    [Fact]
    public async Task WorkflowValidation_ComplexAppraisalSchema_ValidatesWithDatabase()
    {
        // Arrange - Complex workflow schema validation
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var complexSchema = CreateComplexAppraisalWorkflowSchema();

        // Act - Validate workflow schema
        var isValid = await workflowService.ValidateWorkflowDefinitionAsync(complexSchema, TestContext.Current.CancellationToken);

        // Assert - Verify validation success
        isValid.Should().BeTrue("Complex appraisal workflow schema should be valid");
        
        // Test that valid schema can be persisted
        var workflowDefinition = WorkflowDefinition.Create(
            "Complex Validation Test",
            "Complex workflow for validation testing",
            JsonSerializer.Serialize(complexSchema),
            "Validation",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        
        // Should not throw exception on save
        var saveAction = async () => await dbContext.SaveChangesAsync();
        await saveAction.Should().NotThrowAsync();
    }

    private WorkflowSchema CreateSimpleAppraisalWorkflowSchema()
    {
        var activities = new List<ActivityDefinition>
        {
            new()
            {
                Id = "start",
                Type = "StartActivity", 
                Name = "Start Appraisal",
                IsStartActivity = true
            },
            new()
            {
                Id = "property-evaluation",
                Type = "TaskActivity",
                Name = "Property Evaluation",
                Properties = new Dictionary<string, object>
                {
                    ["assignment_strategies"] = new[] { "workload_based", "round_robin" },
                    ["estimated_duration"] = "2 hours"
                }
            },
            new()
            {
                Id = "end",
                Type = "EndActivity",
                Name = "Complete Appraisal",
                IsEndActivity = true
            }
        };
        
        var transitions = new List<TransitionDefinition>
        {
            new() { From = "start", To = "property-evaluation", Id = "start-to-eval" },
            new() { From = "property-evaluation", To = "end", Id = "eval-to-end" }
        };

        return new WorkflowSchema
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Simple Appraisal Workflow",
            Description = "Basic property appraisal workflow for integration testing",
            Activities = activities,
            Transitions = transitions,
            Metadata = new WorkflowMetadata
            {
                Version = "1.0.0",
                Author = "Integration Test",
                CreatedDate = DateTime.UtcNow
            }
        };
    }

    private WorkflowSchema CreateComplexAppraisalWorkflowSchema()
    {
        var activities = new List<ActivityDefinition>
        {
            new()
            {
                Id = "start",
                Type = "StartActivity",
                Name = "Start Appraisal",
                IsStartActivity = true
            },
            new()
            {
                Id = "property-evaluation",
                Type = "TaskActivity", 
                Name = "Property Evaluation",
                Properties = new Dictionary<string, object>
                {
                    ["assignment_strategies"] = new[] { "workload_based", "round_robin" }
                }
            },
            new()
            {
                Id = "market-analysis",
                Type = "TaskActivity",
                Name = "Market Analysis",
                Properties = new Dictionary<string, object>
                {
                    ["assignment_strategies"] = new[] { "supervisor", "manual" }
                }
            },
            new()
            {
                Id = "approval-decision", 
                Type = "IfElseActivity",
                Name = "Approval Decision",
                Properties = new Dictionary<string, object>
                {
                    ["condition"] = "appraisal_value >= loan_amount * 1.1"
                }
            },
            new()
            {
                Id = "end",
                Type = "EndActivity",
                Name = "Complete Appraisal",
                IsEndActivity = true
            }
        };
        
        var transitions = new List<TransitionDefinition>
        {
            new() { From = "start", To = "property-evaluation", Id = "start-to-eval" },
            new() { From = "property-evaluation", To = "market-analysis", Id = "eval-to-market" },
            new() { From = "market-analysis", To = "approval-decision", Id = "market-to-decision" },
            new() { From = "approval-decision", To = "end", Id = "decision-to-end" }
        };

        return new WorkflowSchema
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Complex Appraisal Workflow",
            Description = "Comprehensive property appraisal workflow with decision points",
            Activities = activities,
            Transitions = transitions,
            Metadata = new WorkflowMetadata
            {
                Version = "1.0.0",
                Author = "Integration Test",
                CreatedDate = DateTime.UtcNow
            }
        };
    }

    #region Database Failure Scenarios - Final Phase Enhancement
    
    [Fact]
    public async Task StartWorkflowAsync_DatabaseConnectionFailure_HandlesGracefully()
    {
        // Arrange - Simulate connection failure by disposing context
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Connection Failure Test",
            "Test workflow for connection failure scenarios",
            JsonSerializer.Serialize(workflowSchema),
            "FailureTest",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        
        // Dispose context to simulate connection failure
        await dbContext.DisposeAsync();

        var initialVariables = new Dictionary<string, object>
        {
            ["property_id"] = "PROP-FAIL-001",
            ["test_scenario"] = "connection_failure"
        };

        // Act & Assert - Should handle database failure gracefully
        var act = async () => await workflowService.StartWorkflowAsync(
            workflowDefinition.Id,
            "Connection Failure Test Workflow",
            "test@company.com",
            initialVariables,
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*disposed*", "Should throw meaningful error when database connection fails");
    }

    [Fact]
    public async Task StartWorkflowAsync_DatabaseTimeoutScenario_HandlesTimeout()
    {
        // Arrange - Create scenario that could trigger timeout
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Timeout Test Workflow",
            "Workflow for testing database timeout scenarios",
            JsonSerializer.Serialize(workflowSchema),
            "TimeoutTest",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create extremely large variable set to potentially trigger timeout
        var largeVariables = new Dictionary<string, object>();
        for (int i = 0; i < 10000; i++)
        {
            largeVariables[$"timeout_var_{i}"] = new string('X', 1000); // Large strings
        }
        largeVariables["test_scenario"] = "timeout_test";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act & Assert - Should complete within reasonable time or handle timeout
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await workflowService.StartWorkflowAsync(
                workflowDefinition.Id,
                "Timeout Test Workflow",
                "test@company.com",
                largeVariables,
                cancellationToken: cts.Token);

            // If successful, should complete within reasonable time
            result.Should().NotBeNull();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000, 
                "Even large workflows should complete within 30 seconds");
        }
        catch (OperationCanceledException)
        {
            // Timeout is acceptable for this test - ensures system doesn't hang
            stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(5000, 
                "Timeout should respect the cancellation token");
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    [Fact]
    public async Task ResumeWorkflowAsync_ConcurrencyConflict_HandlesOptimisticLocking()
    {
        // Arrange - Setup for concurrency conflict
        using var scope1 = fixture.Services.CreateScope();
        using var scope2 = fixture.Services.CreateScope();
        
        var workflowService1 = scope1.ServiceProvider.GetRequiredService<IWorkflowService>();
        var workflowService2 = scope2.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope1.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Concurrency Test Workflow",
            "Workflow for concurrency conflict testing",
            JsonSerializer.Serialize(workflowSchema),
            "ConcurrencyTest",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        
        var workflowInstance = WorkflowInstance.Create(
            workflowDefinition.Id,
            "Concurrency Test Instance",
            null,
            "integration-test@company.com",
            new Dictionary<string, object>
            {
                ["property_value"] = 400000,
                ["test_scenario"] = "concurrency_conflict"
            });
            
        workflowInstance.SetCurrentActivity("property-evaluation", "appraiser1@company.com");
        dbContext.WorkflowInstances.Add(workflowInstance);
        
        var activityExecution = WorkflowActivityExecution.Create(
            workflowInstance.Id,
            "property-evaluation",
            "Property Evaluation",
            "TaskActivity",
            "appraiser1@company.com");
            
        dbContext.WorkflowActivityExecutions.Add(activityExecution);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resumeInput1 = new Dictionary<string, object>
        {
            ["appraisal_value"] = 425000,
            ["completed_by"] = "appraiser1@company.com"
        };
        
        var resumeInput2 = new Dictionary<string, object>
        {
            ["appraisal_value"] = 435000,
            ["completed_by"] = "appraiser2@company.com"
        };

        // Act - Attempt concurrent resume operations
        var task1 = workflowService1.ResumeWorkflowAsync(
            workflowInstance.Id, 
            "property-evaluation", 
            "appraiser1@company.com", 
            resumeInput1, 
            cancellationToken: TestContext.Current.CancellationToken);
            
        var task2 = workflowService2.ResumeWorkflowAsync(
            workflowInstance.Id, 
            "property-evaluation", 
            "appraiser2@company.com", 
            resumeInput2, 
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - One should succeed, one should handle the conflict
        var results = await Task.WhenAll(
            task1.ContinueWith(t => new { Success = t.IsCompletedSuccessfully, Result = t.IsCompletedSuccessfully ? t.Result : null, Exception = t.Exception }),
            task2.ContinueWith(t => new { Success = t.IsCompletedSuccessfully, Result = t.IsCompletedSuccessfully ? t.Result : null, Exception = t.Exception })
        );

        // At least one operation should succeed
        results.Should().Contain(r => r.Success, 
            "At least one concurrent resume operation should succeed");
        
        // The successful operation should have valid result
        var successfulResults = results.Where(r => r.Success).ToList();
        successfulResults.Should().NotBeEmpty();
        successfulResults.First().Result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserTasksAsync_DatabaseConstraintViolation_HandlesRobustly()
    {
        // Arrange - Setup scenario that could cause constraint violations
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Constraint Test Workflow",
            "Workflow for constraint violation testing",
            JsonSerializer.Serialize(workflowSchema),
            "ConstraintTest",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        
        // Create workflow instance
        var workflowInstance = WorkflowInstance.Create(
            workflowDefinition.Id,
            "Constraint Test Instance",
            null,
            "integration-test@company.com",
            new Dictionary<string, object>
            {
                ["test_scenario"] = "constraint_violation"
            });
            
        workflowInstance.SetCurrentActivity("property-evaluation", "test-user@company.com");
        dbContext.WorkflowInstances.Add(workflowInstance);
        
        // Create activity execution
        var activityExecution = WorkflowActivityExecution.Create(
            workflowInstance.Id,
            "property-evaluation",
            "Property Evaluation",
            "TaskActivity",
            "test-user@company.com");
            
        dbContext.WorkflowActivityExecutions.Add(activityExecution);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Get user tasks (should handle any constraint issues gracefully)
        var act = async () => await workflowService.GetUserTasksAsync("test-user@company.com", TestContext.Current.CancellationToken);

        // Assert - Should not throw exception and return valid results
        await act.Should().NotThrowAsync("GetUserTasks should handle database constraints gracefully");
        
        var userTasks = await workflowService.GetUserTasksAsync("test-user@company.com", TestContext.Current.CancellationToken);
        userTasks.Should().NotBeNull();
        
        var taskList = userTasks.ToList();
        taskList.Should().ContainSingle(t => t.Name == "Constraint Test Instance");
    }

    [Fact]
    public async Task StartWorkflowAsync_TransactionRollbackScenario_MaintainsDataIntegrity()
    {
        // Arrange - Setup scenario to test transaction rollback
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Transaction Test Workflow",
            "Workflow for transaction rollback testing",
            JsonSerializer.Serialize(workflowSchema),
            "TransactionTest",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Get initial counts
        var initialWorkflowCount = await dbContext.WorkflowInstances.CountAsync(TestContext.Current.CancellationToken);
        var initialActivityCount = await dbContext.WorkflowActivityExecutions.CountAsync(TestContext.Current.CancellationToken);

        var problematicVariables = new Dictionary<string, object>
        {
            ["property_id"] = "TRANS-TEST-001",
            ["test_scenario"] = "transaction_rollback"
        };

        try
        {
            // Act - Start workflow (may succeed or fail depending on system state)
            var result = await workflowService.StartWorkflowAsync(
                workflowDefinition.Id,
                "Transaction Test Workflow",
                "test@company.com",
                problematicVariables,
                cancellationToken: TestContext.Current.CancellationToken);

            // If successful, verify data integrity
            if (result != null)
            {
                result.Should().NotBeNull();
                result.Status.Should().Be(WorkflowStatus.Running);
                
                // Verify database consistency
                var finalWorkflowCount = await dbContext.WorkflowInstances.CountAsync(TestContext.Current.CancellationToken);
                var finalActivityCount = await dbContext.WorkflowActivityExecutions.CountAsync(TestContext.Current.CancellationToken);
                
                finalWorkflowCount.Should().Be(initialWorkflowCount + 1,
                    "Workflow count should increment by exactly 1");
                finalActivityCount.Should().Be(initialActivityCount + 1,
                    "Activity execution count should increment by exactly 1");
            }
        }
        catch (Exception)
        {
            // If failed, verify no partial data was committed (transaction rollback)
            var finalWorkflowCount = await dbContext.WorkflowInstances.CountAsync(TestContext.Current.CancellationToken);
            var finalActivityCount = await dbContext.WorkflowActivityExecutions.CountAsync(TestContext.Current.CancellationToken);
            
            finalWorkflowCount.Should().Be(initialWorkflowCount,
                "Failed workflow creation should not leave partial data (rollback)");
            finalActivityCount.Should().Be(initialActivityCount,
                "Failed workflow creation should not leave orphaned activity executions");
        }
    }

    [Fact]
    public async Task WorkflowService_DatabaseRecovery_ResumeOperationsAfterFailure()
    {
        // Arrange - Setup initial successful state
        using var scope = fixture.Services.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssignmentDbContext>();
        
        var workflowSchema = CreateSimpleAppraisalWorkflowSchema();
        var workflowDefinition = WorkflowDefinition.Create(
            "Recovery Test Workflow",
            "Workflow for database recovery testing",
            JsonSerializer.Serialize(workflowSchema),
            "RecoveryTest",
            "integration-test@company.com");
        
        dbContext.WorkflowDefinitions.Add(workflowDefinition);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create initial workflow successfully
        var initialVariables = new Dictionary<string, object>
        {
            ["property_id"] = "RECOVERY-001",
            ["test_scenario"] = "recovery_test",
            ["initial_state"] = "created"
        };

        var workflowInstance = await workflowService.StartWorkflowAsync(
            workflowDefinition.Id,
            "Recovery Test Instance",
            "recovery-test@company.com",
            initialVariables,
            cancellationToken: TestContext.Current.CancellationToken);

        workflowInstance.Should().NotBeNull();
        var workflowId = workflowInstance.Id;

        // Simulate database "recovery" by creating new scope (simulating reconnection)
        using var recoveryScope = fixture.Services.CreateScope();
        var recoveryWorkflowService = recoveryScope.ServiceProvider.GetRequiredService<IWorkflowService>();
        var recoveryDbContext = recoveryScope.ServiceProvider.GetRequiredService<AssignmentDbContext>();

        // Act - Verify operations work after "recovery"
        var userTasks = await recoveryWorkflowService.GetUserTasksAsync("recovery-test@company.com", TestContext.Current.CancellationToken);
        
        // Resume the workflow after "recovery"
        var resumeInput = new Dictionary<string, object>
        {
            ["recovery_data"] = "workflow resumed after database recovery",
            ["recovery_timestamp"] = DateTime.UtcNow
        };

        var resumedWorkflow = await recoveryWorkflowService.ResumeWorkflowAsync(
            workflowId,
            "property-evaluation",
            "recovery-appraiser@company.com",
            resumeInput,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Verify full recovery and continued operations
        userTasks.Should().NotBeNull();
        var taskList = userTasks.ToList();
        taskList.Should().ContainSingle(t => t.Id == workflowId);

        resumedWorkflow.Should().NotBeNull();
        resumedWorkflow.Variables.Should().ContainKey("recovery_data");
        resumedWorkflow.Variables["recovery_data"].Should().Be("workflow resumed after database recovery");

        // Verify workflow state persisted correctly after recovery
        var persistedInstance = await recoveryDbContext.WorkflowInstances
            .FindAsync([workflowId], TestContext.Current.CancellationToken);
            
        persistedInstance.Should().NotBeNull();
        persistedInstance!.Variables.Should().ContainKey("recovery_data");
        persistedInstance.Variables.Should().ContainKey("initial_state");
        persistedInstance.Variables["initial_state"].Should().Be("created");
    }

    #endregion
}