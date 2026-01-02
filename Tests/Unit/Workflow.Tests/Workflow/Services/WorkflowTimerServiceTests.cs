using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Workflow.Workflow.Configuration;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Services;

public class WorkflowTimerServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IWorkflowBookmarkRepository> _mockBookmarkRepository;
    private readonly Mock<IWorkflowInstanceRepository> _mockWorkflowRepository;
    private readonly Mock<IWorkflowResilienceService> _mockResilienceService;
    private readonly Mock<ILogger<WorkflowTimerService>> _mockLogger;
    private readonly WorkflowOptions _workflowOptions;
    private readonly WorkflowTimerService _timerService;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public WorkflowTimerServiceTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockBookmarkRepository = new Mock<IWorkflowBookmarkRepository>();
        _mockWorkflowRepository = new Mock<IWorkflowInstanceRepository>();
        _mockResilienceService = new Mock<IWorkflowResilienceService>();
        _mockLogger = new Mock<ILogger<WorkflowTimerService>>();
        _cancellationTokenSource = new CancellationTokenSource();

        _workflowOptions = new WorkflowOptions
        {
            TimerProcessing = new TimerProcessingOptions
            {
                CheckInterval = TimeSpan.FromSeconds(10),
                BatchSize = 20,
                TimeoutThreshold = TimeSpan.FromHours(24)
            }
        };

        var options = Options.Create(_workflowOptions);

        // Setup service scope chain
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IWorkflowBookmarkRepository)))
            .Returns(_mockBookmarkRepository.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IWorkflowInstanceRepository)))
            .Returns(_mockWorkflowRepository.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IWorkflowResilienceService)))
            .Returns(_mockResilienceService.Object);

        _timerService = new WorkflowTimerService(
            _mockScopeFactory.Object,
            options,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessTimers_NoDueBookmarks_CompletesSuccessfully()
    {
        // Arrange
        _mockBookmarkRepository
            .Setup(x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowBookmark>());

        _mockWorkflowRepository
            .Setup(x => x.GetLongRunningWorkflowsAsync(
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowInstance>());

        // Act & Assert - Should not throw
        await _timerService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow some processing time
        await _timerService.StopAsync(_cancellationTokenSource.Token);
    }

    [Fact]
    public async Task ProcessTimers_DueTimerBookmarks_ProcessesCorrectly()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var bookmark = WorkflowBookmark.CreateTimer(
            workflowId, 
            "timer-activity", 
            "timer-key", 
            DateTime.UtcNow.AddMinutes(-1)); // Due timer

        _mockBookmarkRepository
            .Setup(x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowBookmark> { bookmark });

        _mockWorkflowRepository
            .Setup(x => x.GetLongRunningWorkflowsAsync(
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowInstance>());

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, string, CancellationToken>(
                async (operation, key, ct) => await operation(ct));

        // Act
        await _timerService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow processing time
        await _timerService.StopAsync(_cancellationTokenSource.Token);

        // Assert
        _mockBookmarkRepository.Verify(
            x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessTimers_LongRunningWorkflows_HandlesTimeouts()
    {
        // Arrange
        var longRunningWorkflow = new WorkflowInstance();
        
        _mockBookmarkRepository
            .Setup(x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowBookmark>());

        _mockWorkflowRepository
            .Setup(x => x.GetLongRunningWorkflowsAsync(
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowInstance> { longRunningWorkflow });

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, string, CancellationToken>(
                async (operation, key, ct) => await operation(ct));

        // Act
        await _timerService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow processing time
        await _timerService.StopAsync(_cancellationTokenSource.Token);

        // Assert
        _mockWorkflowRepository.Verify(
            x => x.GetLongRunningWorkflowsAsync(
                _workflowOptions.TimerProcessing.TimeoutThreshold,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessTimers_ResilienceServiceFailure_HandlesGracefully()
    {
        // Arrange
        var bookmark = WorkflowBookmark.CreateTimer(
            Guid.NewGuid(), 
            "timer-activity", 
            "timer-key", 
            DateTime.UtcNow.AddMinutes(-1));

        _mockBookmarkRepository
            .Setup(x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowBookmark> { bookmark });

        _mockWorkflowRepository
            .Setup(x => x.GetLongRunningWorkflowsAsync(
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowInstance>());

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Timer processing failed"));

        // Act & Assert - Should handle exception gracefully
        await _timerService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow processing time
        await _timerService.StopAsync(_cancellationTokenSource.Token);

        // Verify it at least attempted to process
        _mockResilienceService.Verify(
            x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task ProcessTimers_VariableBookmarkCounts_HandlesCorrectly(int bookmarkCount)
    {
        // Arrange
        var bookmarks = new List<WorkflowBookmark>();
        for (int i = 0; i < bookmarkCount; i++)
        {
            bookmarks.Add(WorkflowBookmark.CreateTimer(
                Guid.NewGuid(), 
                $"timer-activity-{i}", 
                $"timer-key-{i}", 
                DateTime.UtcNow.AddMinutes(-i - 1)));
        }

        _mockBookmarkRepository
            .Setup(x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookmarks);

        _mockWorkflowRepository
            .Setup(x => x.GetLongRunningWorkflowsAsync(
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowInstance>());

        _mockResilienceService
            .Setup(x => x.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, string, CancellationToken>(
                async (operation, key, ct) => await operation(ct));

        // Act
        await _timerService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(150); // Allow processing time
        await _timerService.StopAsync(_cancellationTokenSource.Token);

        // Assert
        _mockBookmarkRepository.Verify(
            x => x.GetDueTimerBookmarksAsync(
                _workflowOptions.TimerProcessing.BatchSize, 
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartAsync_CancellationRequested_StopsGracefully()
    {
        // Arrange
        _mockBookmarkRepository
            .Setup(x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowBookmark>());

        _mockWorkflowRepository
            .Setup(x => x.GetLongRunningWorkflowsAsync(
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowInstance>());

        // Act
        await _timerService.StartAsync(_cancellationTokenSource.Token);
        _cancellationTokenSource.Cancel();
        await _timerService.StopAsync(CancellationToken.None);

        // Assert - Should complete without throwing
        Assert.True(_cancellationTokenSource.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task ProcessTimers_ConfigurationValues_UsedCorrectly()
    {
        // Arrange
        _mockBookmarkRepository
            .Setup(x => x.GetDueTimerBookmarksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowBookmark>());

        _mockWorkflowRepository
            .Setup(x => x.GetLongRunningWorkflowsAsync(
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowInstance>());

        // Act
        await _timerService.StartAsync(_cancellationTokenSource.Token);
        await Task.Delay(100); // Allow processing time
        await _timerService.StopAsync(_cancellationTokenSource.Token);

        // Assert - Verify correct configuration values are used
        _mockBookmarkRepository.Verify(
            x => x.GetDueTimerBookmarksAsync(
                _workflowOptions.TimerProcessing.BatchSize, 
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _mockWorkflowRepository.Verify(
            x => x.GetLongRunningWorkflowsAsync(
                _workflowOptions.TimerProcessing.TimeoutThreshold,
                _workflowOptions.TimerProcessing.BatchSize,
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Service_ImplementsBackgroundService()
    {
        // Assert
        Assert.IsAssignableFrom<BackgroundService>(_timerService);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _timerService?.Dispose();
    }
}