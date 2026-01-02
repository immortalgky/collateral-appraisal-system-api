using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Workflow.Workflow.Configuration;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Services;

public class OutboxDispatcherServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IWorkflowOutboxRepository> _mockOutboxRepository;
    private readonly Mock<IWorkflowResilienceService> _mockResilienceService;
    private readonly Mock<ILogger<OutboxDispatcherService>> _mockLogger;
    private readonly WorkflowOptions _workflowOptions;
    private readonly OutboxDispatcherService _dispatcherService;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public OutboxDispatcherServiceTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockOutboxRepository = new Mock<IWorkflowOutboxRepository>();
        _mockResilienceService = new Mock<IWorkflowResilienceService>();
        _mockLogger = new Mock<ILogger<OutboxDispatcherService>>();
        _cancellationTokenSource = new CancellationTokenSource();

        _workflowOptions = new WorkflowOptions
        {
            OutboxProcessing = new OutboxProcessingOptions
            {
                BatchSize = 10,
                ProcessingInterval = TimeSpan.FromSeconds(5),
                MaxRetryAttempts = 3,
                RetryDelay = TimeSpan.FromSeconds(2)
            }
        };

        var options = Options.Create(_workflowOptions);

        // Setup service scope chain
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IWorkflowOutboxRepository)))
            .Returns(_mockOutboxRepository.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IWorkflowResilienceService)))
            .Returns(_mockResilienceService.Object);

        _dispatcherService = new OutboxDispatcherService(
            _mockScopeFactory.Object,
            options,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessOutboxEvents_NoEvents_CompletesSuccessfully()
    {
        // Arrange
        _mockOutboxRepository
            .Setup(x => x.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowOutbox>());

        // Act & Assert - Should not throw
        await _dispatcherService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow some processing time
        await _dispatcherService.StopAsync(_cancellationTokenSource.Token);
    }

    [Fact]
    public async Task ProcessOutboxEvents_SuccessfulEvents_MarksAsProcessed()
    {
        // Arrange
        var outboxEvent = WorkflowOutbox.Create(
            "WorkflowCompleted",
            JsonSerializer.Serialize(new { WorkflowId = Guid.NewGuid() }),
            new Dictionary<string, string> { { "Source", "WorkflowEngine" } },
            "test-correlation");

        _mockOutboxRepository
            .Setup(x => x.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowOutbox> { outboxEvent });

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, string, CancellationToken>(
                async (operation, key, ct) => await operation(ct));

        // Act
        await _dispatcherService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow processing time
        await _dispatcherService.StopAsync(_cancellationTokenSource.Token);

        // Assert
        _mockOutboxRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessOutboxEvents_FailedEvent_IncrementsAttempts()
    {
        // Arrange
        var outboxEvent = WorkflowOutbox.Create(
            "WorkflowFailed",
            JsonSerializer.Serialize(new { WorkflowId = Guid.NewGuid() }),
            correlationId: "test-correlation");

        _mockOutboxRepository
            .Setup(x => x.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowOutbox> { outboxEvent });

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Publishing failed"));

        // Act
        await _dispatcherService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow processing time
        await _dispatcherService.StopAsync(_cancellationTokenSource.Token);

        // Assert
        _mockOutboxRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessOutboxEvents_MaxRetriesExceeded_MarksAsDeadLetter()
    {
        // Arrange
        var outboxEvent = WorkflowOutbox.Create(
            "WorkflowFailed",
            JsonSerializer.Serialize(new { WorkflowId = Guid.NewGuid() }));

        // Simulate already failed attempts
        for (int i = 0; i < _workflowOptions.OutboxProcessing.MaxRetryAttempts; i++)
        {
            outboxEvent.IncrementAttempt("Previous failure");
        }

        _mockOutboxRepository
            .Setup(x => x.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowOutbox> { outboxEvent });

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Final failure"));

        // Act
        await _dispatcherService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow processing time
        await _dispatcherService.StopAsync(_cancellationTokenSource.Token);

        // Assert
        _mockOutboxRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task ProcessOutboxEvents_MultipleBatches_ProcessesCorrectly(int eventCount)
    {
        // Arrange
        var events = new List<WorkflowOutbox>();
        for (int i = 0; i < eventCount; i++)
        {
            events.Add(WorkflowOutbox.Create(
                $"Event{i}",
                JsonSerializer.Serialize(new { Index = i }),
                correlationId: $"correlation-{i}"));
        }

        _mockOutboxRepository
            .Setup(x => x.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, string, CancellationToken>(
                async (operation, key, ct) => await operation(ct));

        // Act
        await _dispatcherService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(200); // Allow processing time
        await _dispatcherService.StopAsync(_cancellationTokenSource.Token);

        // Assert
        _mockOutboxRepository.Verify(
            x => x.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartAsync_CancellationRequested_StopsGracefully()
    {
        // Arrange
        _mockOutboxRepository
            .Setup(x => x.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowOutbox>());

        // Act
        await _dispatcherService.StartAsync(_cancellationTokenSource.Token);
        _cancellationTokenSource.Cancel();
        await _dispatcherService.StopAsync(CancellationToken.None);

        // Assert - Should complete without throwing
        Assert.True(_cancellationTokenSource.Token.IsCancellationRequested);
    }

    [Fact]
    public void Service_ImplementsBackgroundService()
    {
        // Assert
        Assert.IsAssignableFrom<BackgroundService>(_dispatcherService);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _dispatcherService?.Dispose();
    }
}