using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Workflow.Data;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Services;

public class WorkflowFaultHandlerTests
{
    private readonly Mock<WorkflowDbContext> _mockContext;
    private readonly Mock<IWorkflowExecutionLogRepository> _mockExecutionLogRepository;
    private readonly Mock<IWorkflowInstanceRepository> _mockWorkflowRepository;
    private readonly Mock<IWorkflowOutboxRepository> _mockOutboxRepository;
    private readonly Mock<ILogger<WorkflowFaultHandler>> _mockLogger;
    private readonly WorkflowFaultHandler _faultHandler;

    public WorkflowFaultHandlerTests()
    {
        _mockContext = new Mock<WorkflowDbContext>();
        _mockExecutionLogRepository = new Mock<IWorkflowExecutionLogRepository>();
        _mockWorkflowRepository = new Mock<IWorkflowInstanceRepository>();
        _mockOutboxRepository = new Mock<IWorkflowOutboxRepository>();
        _mockLogger = new Mock<ILogger<WorkflowFaultHandler>>();

        _faultHandler = new WorkflowFaultHandler(
            _mockContext.Object,
            _mockExecutionLogRepository.Object,
            _mockWorkflowRepository.Object,
            _mockOutboxRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task HandleWorkflowStartupFaultAsync_TransientError_ReturnsRetryResult()
    {
        // Arrange
        var faultContext = new StartWorkflowFaultContext(
            Guid.NewGuid(),
            "Test Workflow",
            "test-user",
            new TimeoutException("Database timeout"),
            1);

        // Act
        var result = await _faultHandler.HandleWorkflowStartupFaultAsync(faultContext);

        // Assert
        Assert.True(result.ShouldRetry);
        Assert.False(result.SuspendWorkflow);
        Assert.False(result.RequiresManualIntervention);
        Assert.NotNull(result.RetryDelay);
        Assert.NotNull(result.RecommendedAction);
    }

    [Fact]
    public async Task HandleWorkflowStartupFaultAsync_NonRetryableError_ReturnsNoRetryResult()
    {
        // Arrange
        var faultContext = new StartWorkflowFaultContext(
            Guid.NewGuid(),
            "Test Workflow",
            "test-user",
            new ArgumentException("Invalid workflow definition"),
            1);

        // Act
        var result = await _faultHandler.HandleWorkflowStartupFaultAsync(faultContext);

        // Assert
        Assert.False(result.ShouldRetry);
        Assert.False(result.SuspendWorkflow);
        Assert.True(result.RequiresManualIntervention);
        Assert.Null(result.RetryDelay);
        Assert.Equal("Validate input parameters and workflow definition", result.RecommendedAction);
    }

    [Fact]
    public async Task HandleWorkflowStartupFaultAsync_MaxAttemptsReached_ReturnsNoRetryResult()
    {
        // Arrange
        var faultContext = new StartWorkflowFaultContext(
            Guid.NewGuid(),
            "Test Workflow",
            "test-user",
            new TimeoutException("Database timeout"),
            3); // Max attempts reached

        // Act
        var result = await _faultHandler.HandleWorkflowStartupFaultAsync(faultContext);

        // Assert
        Assert.False(result.ShouldRetry);
        Assert.False(result.SuspendWorkflow);
        Assert.True(result.RequiresManualIntervention);
        Assert.Null(result.RetryDelay);
    }

    [Fact]
    public async Task HandleActivityExecutionFaultAsync_RetryableError_ReturnsRetryResult()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var activityId = "test-activity";
        var faultContext = new ActivityFaultContext(
            workflowId,
            activityId,
            "TestActivity",
            new HttpRequestException("Network error"),
            1,
            new Dictionary<string, object>());

        // Mock should suspend check to return false
        _mockExecutionLogRepository
            .Setup(x => x.GetByEventTypeAsync(
                ExecutionLogEvent.ActivityFailed,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowExecutionLog>());

        // Act
        var result = await _faultHandler.HandleActivityExecutionFaultAsync(faultContext);

        // Assert
        Assert.True(result.ShouldRetry);
        Assert.False(result.SuspendWorkflow);
        Assert.False(result.RequiresManualIntervention);
        Assert.NotNull(result.RetryDelay);
    }

    [Fact]
    public async Task HandleActivityExecutionFaultAsync_TooManyFailures_SuspendsWorkflow()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var activityId = "test-activity";
        var faultContext = new ActivityFaultContext(
            workflowId,
            activityId,
            "TestActivity",
            new HttpRequestException("Network error"),
            1,
            new Dictionary<string, object>());

        // Mock workflow for suspension
        var workflow = new WorkflowInstance();
        _mockWorkflowRepository
            .Setup(x => x.GetForUpdateAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        // Mock many recent failures to trigger suspension
        var recentFailures = Enumerable.Range(0, 6) // More than suspension threshold
            .Select(i => WorkflowExecutionLog.ActivityFailed(workflowId, activityId, "test-user", "Error"))
            .ToList();

        _mockExecutionLogRepository
            .Setup(x => x.GetByEventTypeAsync(
                ExecutionLogEvent.ActivityFailed,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentFailures);

        // Act
        var result = await _faultHandler.HandleActivityExecutionFaultAsync(faultContext);

        // Assert
        Assert.False(result.ShouldRetry);
        Assert.True(result.SuspendWorkflow);
        Assert.True(result.RequiresManualIntervention);
    }

    [Fact]
    public async Task HandleExternalCallFaultAsync_HttpError_ReturnsRetryResult()
    {
        // Arrange
        var faultContext = new ExternalCallFaultContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-activity",
            ExternalCallType.HttpRequest,
            "https://api.example.com/test",
            new HttpRequestException("Service unavailable"),
            1);

        // Act
        var result = await _faultHandler.HandleExternalCallFaultAsync(faultContext);

        // Assert
        Assert.True(result.ShouldRetry);
        Assert.False(result.SuspendWorkflow);
        Assert.False(result.RequiresManualIntervention);
        Assert.NotNull(result.RetryDelay);
        Assert.Contains("external service", result.RecommendedAction);
    }

    [Fact]
    public async Task HandleExternalCallFaultAsync_MaxRetriesReached_ReturnsNoRetryResult()
    {
        // Arrange
        var faultContext = new ExternalCallFaultContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-activity",
            ExternalCallType.HttpRequest,
            "https://api.example.com/test",
            new HttpRequestException("Service unavailable"),
            5); // Max retries reached

        // Act
        var result = await _faultHandler.HandleExternalCallFaultAsync(faultContext);

        // Assert
        Assert.False(result.ShouldRetry);
        Assert.False(result.SuspendWorkflow);
        Assert.True(result.RequiresManualIntervention);
        Assert.Null(result.RetryDelay);
    }

    [Fact]
    public async Task HandleWorkflowResumeFaultAsync_RetryableError_ReturnsRetryResult()
    {
        // Arrange
        var faultContext = new WorkflowResumeFaultContext(
            Guid.NewGuid(),
            "test-activity",
            "test-bookmark-key",
            new TimeoutException("Database timeout"),
            1);

        // Act
        var result = await _faultHandler.HandleWorkflowResumeFaultAsync(faultContext);

        // Assert
        Assert.True(result.ShouldRetry);
        Assert.False(result.SuspendWorkflow);
        Assert.False(result.RequiresManualIntervention);
        Assert.NotNull(result.RetryDelay);
        Assert.Equal("Check workflow state and bookmark validity", result.RecommendedAction);
    }

    [Fact]
    public async Task ShouldSuspendWorkflowAsync_FewFailures_ReturnsFalse()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var recentFailures = new List<WorkflowExecutionLog>
        {
            WorkflowExecutionLog.ActivityFailed(workflowId, "activity1", "user", "Error 1"),
            WorkflowExecutionLog.ActivityFailed(workflowId, "activity2", "user", "Error 2")
        };

        _mockExecutionLogRepository
            .Setup(x => x.GetByEventTypeAsync(
                ExecutionLogEvent.ActivityFailed,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentFailures);

        // Act
        var shouldSuspend = await _faultHandler.ShouldSuspendWorkflowAsync(workflowId, "activity-fault");

        // Assert
        Assert.False(shouldSuspend);
    }

    [Fact]
    public async Task ShouldSuspendWorkflowAsync_ManyFailures_ReturnsTrue()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var recentFailures = Enumerable.Range(0, 6) // More than suspension threshold
            .Select(i => WorkflowExecutionLog.ActivityFailed(workflowId, $"activity{i}", "user", $"Error {i}"))
            .ToList();

        _mockExecutionLogRepository
            .Setup(x => x.GetByEventTypeAsync(
                ExecutionLogEvent.ActivityFailed,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentFailures);

        // Act
        var shouldSuspend = await _faultHandler.ShouldSuspendWorkflowAsync(workflowId, "activity-fault");

        // Assert
        Assert.True(shouldSuspend);
    }

    [Fact]
    public async Task CreateCompensationPlanAsync_WorkflowNotFound_ThrowsException()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        _mockWorkflowRepository
            .Setup(x => x.GetWithExecutionsAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _faultHandler.CreateCompensationPlanAsync(workflowId));

        Assert.Contains("not found", exception.Message);
    }

    [Theory]
    [InlineData("EmailActivity")]
    [InlineData("DataUpdateActivity")]
    [InlineData("UnknownActivity")]
    public async Task CreateCompensationPlanAsync_WithCompletedActivities_CreatesValidPlan(string activityType)
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var workflow = new WorkflowInstance();
        var activityExecution = new WorkflowActivityExecution
        {
            Id = Guid.NewGuid(),
            ActivityId = "test-activity",
            ActivityName = "Test Activity",
            ActivityType = activityType,
            Status = ActivityExecutionStatus.Completed,
            CompletedOn = DateTime.UtcNow
        };
        workflow.ActivityExecutions.Add(activityExecution);

        _mockWorkflowRepository
            .Setup(x => x.GetWithExecutionsAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        // Act
        var plan = await _faultHandler.CreateCompensationPlanAsync(workflowId);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(workflowId, plan.WorkflowInstanceId);
        Assert.NotEmpty(plan.Steps);
        Assert.NotNull(plan.Strategy);
    }
}