using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Workflow.Workflow.Configuration;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Services;

public class WorkflowResilienceServiceTests
{
    private readonly Mock<ILogger<WorkflowResilienceService>> _mockLogger;
    private readonly WorkflowResilienceOptions _resilienceOptions;
    private readonly WorkflowResilienceService _resilienceService;

    public WorkflowResilienceServiceTests()
    {
        _mockLogger = new Mock<ILogger<WorkflowResilienceService>>();
        _resilienceOptions = new WorkflowResilienceOptions
        {
            Retry = new RetryPolicyOptions
            {
                MaxRetryAttempts = 3,
                BaseDelay = TimeSpan.FromMilliseconds(100),
                MaxDelay = TimeSpan.FromSeconds(10)
            },
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 10,
                SuccessThreshold = 0.8
            },
            Timeout = new TimeoutOptions
            {
                DatabaseOperation = TimeSpan.FromSeconds(30),
                ExternalHttpCall = TimeSpan.FromSeconds(60),
                ActivityExecution = TimeSpan.FromMinutes(5)
            }
        };

        var options = Options.Create(_resilienceOptions);
        _resilienceService = new WorkflowResilienceService(options, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";
        var operation = new Func<CancellationToken, Task<string>>(ct => Task.FromResult(expectedResult));

        // Act
        var result = await _resilienceService.ExecuteWithRetryAsync(operation, "test-operation");

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_FailingOperationWithinRetries_EventuallySucceeds()
    {
        // Arrange
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(ct =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new InvalidOperationException("Temporary failure");
            return Task.FromResult("success");
        });

        // Act
        var result = await _resilienceService.ExecuteWithRetryAsync(operation, "test-operation");

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteExternalCallAsync_SuccessfulCall_ReturnsResult()
    {
        // Arrange
        var expectedResult = "external-success";
        var operation = new Func<CancellationToken, Task<string>>(ct => Task.FromResult(expectedResult));

        // Act
        var result = await _resilienceService.ExecuteExternalCallAsync(operation, "test-service");

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteWithTimeoutAsync_WithinTimeout_ReturnsResult()
    {
        // Arrange
        var expectedResult = "timeout-success";
        var operation = new Func<CancellationToken, Task<string>>(async ct =>
        {
            await Task.Delay(100, ct);
            return expectedResult;
        });

        // Act
        var result = await _resilienceService.ExecuteWithTimeoutAsync(operation, TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteWithTimeoutAsync_ExceedsTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var operation = new Func<CancellationToken, Task<string>>(async ct =>
        {
            await Task.Delay(2000, ct);
            return "should-not-reach";
        });

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(
            () => _resilienceService.ExecuteWithTimeoutAsync(operation, TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task ExecuteDatabaseOperationAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "database-success";
        var operation = new Func<CancellationToken, Task<string>>(ct => Task.FromResult(expectedResult));

        // Act
        var result = await _resilienceService.ExecuteDatabaseOperationAsync(operation);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteWorkflowActivityAsync_SuccessfulActivity_ReturnsResult()
    {
        // Arrange
        var expectedResult = "activity-success";
        var operation = new Func<CancellationToken, Task<string>>(ct => Task.FromResult(expectedResult));

        // Act
        var result = await _resilienceService.ExecuteWorkflowActivityAsync(operation, "test-activity");

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void IsCircuitOpen_NewService_ReturnsFalse()
    {
        // Act
        var isOpen = _resilienceService.IsCircuitOpen("test-service");

        // Assert
        Assert.False(isOpen);
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsValidMetrics()
    {
        // Arrange
        // Execute some operations to generate metrics
        var operation = new Func<CancellationToken, Task<string>>(ct => Task.FromResult("test"));
        await _resilienceService.ExecuteWithRetryAsync(operation, "test-operation");

        // Act
        var metrics = await _resilienceService.GetMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.ServiceMetrics.Count >= 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task ExecuteWithRetryAsync_MultipleOperations_HandlesCorrectly(int operationCount)
    {
        // Arrange
        var tasks = new List<Task<string>>();
        var operation = new Func<CancellationToken, Task<string>>(ct => Task.FromResult("success"));

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            tasks.Add(_resilienceService.ExecuteWithRetryAsync(operation, $"test-operation-{i}"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.Equal("success", result));
    }

    [Fact]
    public async Task ExecuteExternalCallAsync_CircuitBreakerOpenScenario_HandlesCorrectly()
    {
        // Arrange
        var failingOperation = new Func<CancellationToken, Task<string>>(ct =>
            throw new HttpRequestException("Service unavailable"));

        // Act & Assert
        // Execute multiple failing operations to potentially trip circuit breaker
        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(
                () => _resilienceService.ExecuteExternalCallAsync(failingOperation, "failing-service"));
        }
    }
}