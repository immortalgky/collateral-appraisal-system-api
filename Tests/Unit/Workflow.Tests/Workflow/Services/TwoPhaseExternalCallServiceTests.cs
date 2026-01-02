using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http;
using Workflow.Data;
using Workflow.Workflow.Models;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Services;

public class TwoPhaseExternalCallServiceTests : IDisposable
{
    private readonly Mock<WorkflowDbContext> _mockContext;
    private readonly Mock<HttpClient> _mockHttpClient;
    private readonly Mock<IWorkflowResilienceService> _mockResilienceService;
    private readonly Mock<ILogger<TwoPhaseExternalCallService>> _mockLogger;
    private readonly Mock<DbSet<WorkflowExternalCall>> _mockExternalCallSet;
    private readonly TwoPhaseExternalCallService _externalCallService;

    public TwoPhaseExternalCallServiceTests()
    {
        _mockContext = new Mock<WorkflowDbContext>();
        _mockHttpClient = new Mock<HttpClient>();
        _mockResilienceService = new Mock<IWorkflowResilienceService>();
        _mockLogger = new Mock<ILogger<TwoPhaseExternalCallService>>();
        _mockExternalCallSet = new Mock<DbSet<WorkflowExternalCall>>();

        _mockContext.Setup(x => x.Set<WorkflowExternalCall>()).Returns(_mockExternalCallSet.Object);

        _externalCallService = new TwoPhaseExternalCallService(
            _mockContext.Object,
            _mockHttpClient.Object,
            _mockResilienceService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RecordExternalCallIntentAsync_NewCall_CreatesAndReturnsCall()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var activityId = "test-activity";
        var endpoint = "https://api.example.com/test";

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowExternalCall?)null);

        // Act
        var result = await _externalCallService.RecordExternalCallIntentAsync(
            workflowId, activityId, ExternalCallType.HttpRequest, endpoint, "GET");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflowId, result.WorkflowInstanceId);
        Assert.Equal(activityId, result.ActivityId);
        Assert.Equal(ExternalCallType.HttpRequest, result.Type);
        Assert.Equal(endpoint, result.Endpoint);
        Assert.Equal("GET", result.Method);
        Assert.Equal(ExternalCallStatus.Pending, result.Status);

        _mockExternalCallSet.Verify(x => x.Add(It.IsAny<WorkflowExternalCall>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordExternalCallIntentAsync_ExistingCall_ReturnsExistingCall()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var activityId = "test-activity";
        var endpoint = "https://api.example.com/test";
        var existingCall = WorkflowExternalCall.Create(
            workflowId, activityId, ExternalCallType.HttpRequest, endpoint, "GET");

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCall);

        // Act
        var result = await _externalCallService.RecordExternalCallIntentAsync(
            workflowId, activityId, ExternalCallType.HttpRequest, endpoint, "GET");

        // Assert
        Assert.Same(existingCall, result);
        _mockExternalCallSet.Verify(x => x.Add(It.IsAny<WorkflowExternalCall>()), Times.Never);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteExternalCallAsync_CallNotFound_ReturnsFailureResult()
    {
        // Arrange
        var callId = Guid.NewGuid();

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowExternalCall?)null);

        // Act
        var result = await _externalCallService.ExecuteExternalCallAsync(callId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("External call not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteExternalCallAsync_CallNotPending_ReturnsFailureResult()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var externalCall = WorkflowExternalCall.Create(
            Guid.NewGuid(), "test-activity", ExternalCallType.HttpRequest, "https://api.example.com", "GET");
        externalCall.MarkAsStarted(); // Not pending anymore

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalCall);

        // Act
        var result = await _externalCallService.ExecuteExternalCallAsync(callId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not pending", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteExternalCallAsync_SuccessfulHttpCall_ReturnsSuccessResult()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var externalCall = WorkflowExternalCall.Create(
            Guid.NewGuid(), "test-activity", ExternalCallType.HttpRequest, "https://api.example.com", "GET");

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalCall);

        // Mock resilience service to execute the operation directly
        _mockResilienceService
            .Setup(x => x.ExecuteExternalCallAsync(
                It.IsAny<Func<CancellationToken, Task<ExternalCallResult>>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ExternalCallResult>>, string, CancellationToken>(
                async (operation, serviceKey, ct) => await operation(ct));

        // Act
        var result = await _externalCallService.ExecuteExternalCallAsync(callId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ResponsePayload);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteExternalCallAsync_HttpCallThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var externalCall = WorkflowExternalCall.Create(
            Guid.NewGuid(), "test-activity", ExternalCallType.HttpRequest, "https://api.example.com", "GET");

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalCall);

        // Mock resilience service to throw exception
        _mockResilienceService
            .Setup(x => x.ExecuteExternalCallAsync(
                It.IsAny<Func<CancellationToken, Task<ExternalCallResult>>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _externalCallService.ExecuteExternalCallAsync(callId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network error", result.ErrorMessage);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteExternalCallAsync_OperationCancelled_ReturnsTimeoutResult()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var externalCall = WorkflowExternalCall.Create(
            Guid.NewGuid(), "test-activity", ExternalCallType.HttpRequest, "https://api.example.com", "GET");

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalCall);

        // Mock resilience service to throw cancellation exception
        _mockResilienceService
            .Setup(x => x.ExecuteExternalCallAsync(
                It.IsAny<Func<CancellationToken, Task<ExternalCallResult>>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _externalCallService.ExecuteExternalCallAsync(callId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("cancelled or timed out", result.ErrorMessage);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CompleteExternalCallAsync_CallFound_MarksAsCompletedAndReturnsSuccess()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var responsePayload = "Success response";
        var duration = TimeSpan.FromSeconds(2);
        var externalCall = WorkflowExternalCall.Create(
            Guid.NewGuid(), "test-activity", ExternalCallType.HttpRequest, "https://api.example.com", "GET");

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalCall);

        // Act
        var result = await _externalCallService.CompleteExternalCallAsync(callId, responsePayload, duration);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(responsePayload, result.ResponsePayload);
        Assert.Equal(duration, result.Duration);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailExternalCallAsync_CallFound_MarksAsFailedAndReturnsFailure()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var errorMessage = "Service unavailable";
        var duration = TimeSpan.FromSeconds(1);
        var externalCall = WorkflowExternalCall.Create(
            Guid.NewGuid(), "test-activity", ExternalCallType.HttpRequest, "https://api.example.com", "GET");

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalCall);

        // Act
        var result = await _externalCallService.FailExternalCallAsync(callId, errorMessage, duration);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Equal(duration, result.Duration);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ExternalCallType.ThirdPartyApi)]
    [InlineData(ExternalCallType.EmailService)]
    [InlineData(ExternalCallType.NotificationService)]
    public async Task ExecuteExternalCallAsync_DifferentCallTypes_HandlesCorrectly(ExternalCallType callType)
    {
        // Arrange
        var callId = Guid.NewGuid();
        var externalCall = WorkflowExternalCall.Create(
            Guid.NewGuid(), "test-activity", callType, "https://api.example.com", "POST");

        _mockExternalCallSet
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<WorkflowExternalCall, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalCall);

        // Mock resilience service to execute the operation directly
        _mockResilienceService
            .Setup(x => x.ExecuteExternalCallAsync(
                It.IsAny<Func<CancellationToken, Task<ExternalCallResult>>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ExternalCallResult>>, string, CancellationToken>(
                async (operation, serviceKey, ct) => await operation(ct));

        // Act
        var result = await _externalCallService.ExecuteExternalCallAsync(callId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ResponsePayload);
    }

    public void Dispose()
    {
        _mockHttpClient.Object?.Dispose();
    }
}