using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Workflow.Data;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

public class TwoPhaseExternalCallService : ITwoPhaseExternalCallService
{
    private readonly WorkflowDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly ILogger<TwoPhaseExternalCallService> _logger;

    public TwoPhaseExternalCallService(
        WorkflowDbContext context,
        HttpClient httpClient,
        IWorkflowResilienceService resilienceService,
        ILogger<TwoPhaseExternalCallService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _resilienceService = resilienceService;
        _logger = logger;
    }

    public async Task<WorkflowExternalCall> RecordExternalCallIntentAsync(
        Guid workflowInstanceId,
        string activityId,
        ExternalCallType type,
        string endpoint,
        string method,
        string? requestPayload = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recording external call intent for workflow {WorkflowInstanceId}, activity {ActivityId}",
            workflowInstanceId, activityId);

        // Check for existing call with same idempotency characteristics
        var existingCall = await _context.Set<WorkflowExternalCall>()
            .FirstOrDefaultAsync(c => 
                c.WorkflowInstanceId == workflowInstanceId &&
                c.ActivityId == activityId &&
                c.Endpoint == endpoint &&
                c.Method == method, cancellationToken);

        if (existingCall != null)
        {
            _logger.LogInformation("External call already exists for workflow {WorkflowInstanceId}, activity {ActivityId}",
                workflowInstanceId, activityId);
            return existingCall;
        }

        var externalCall = WorkflowExternalCall.Create(
            workflowInstanceId,
            activityId,
            type,
            endpoint,
            method,
            requestPayload,
            headers);

        _context.Set<WorkflowExternalCall>().Add(externalCall);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("External call intent recorded with ID {ExternalCallId}",
            externalCall.Id);

        return externalCall;
    }

    public async Task<ExternalCallResult> ExecuteExternalCallAsync(
        Guid externalCallId,
        CancellationToken cancellationToken = default)
    {
        var externalCall = await _context.Set<WorkflowExternalCall>()
            .FirstOrDefaultAsync(c => c.Id == externalCallId, cancellationToken);

        if (externalCall == null)
        {
            return new ExternalCallResult(false, null, "External call not found");
        }

        if (externalCall.Status != ExternalCallStatus.Pending)
        {
            _logger.LogWarning("External call {ExternalCallId} is not in pending status: {Status}",
                externalCallId, externalCall.Status);
            return new ExternalCallResult(false, null, $"External call is not pending: {externalCall.Status}");
        }

        _logger.LogInformation("Executing external call {ExternalCallId} to {Endpoint}",
            externalCallId, externalCall.Endpoint);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Mark as started
            externalCall.MarkAsStarted();
            await _context.SaveChangesAsync(cancellationToken);

            // Execute the external call with resilience policies
            var serviceKey = GetServiceKey(externalCall);
            var result = await _resilienceService.ExecuteExternalCallAsync(
                async (ct) => await ExecuteCallByType(externalCall, ct),
                serviceKey,
                cancellationToken);
            
            stopwatch.Stop();

            if (result.Success)
            {
                externalCall.MarkAsCompleted(result.ResponsePayload ?? "", stopwatch.Elapsed);
                _logger.LogInformation("External call {ExternalCallId} completed successfully in {Duration}ms",
                    externalCallId, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                externalCall.MarkAsFailed(result.ErrorMessage ?? "Unknown error", stopwatch.Elapsed);
                _logger.LogWarning("External call {ExternalCallId} failed: {ErrorMessage}",
                    externalCallId, result.ErrorMessage);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            externalCall.MarkAsTimedOut(stopwatch.Elapsed);
            await _context.SaveChangesAsync(CancellationToken.None);
            
            _logger.LogWarning("External call {ExternalCallId} was cancelled/timed out",
                externalCallId);
            
            return new ExternalCallResult(false, null, "Operation was cancelled or timed out", 
                stopwatch.Elapsed, externalCall.CanRetry(3));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            externalCall.MarkAsFailed(ex.Message, stopwatch.Elapsed);
            await _context.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex, "Error executing external call {ExternalCallId}",
                externalCallId);

            return new ExternalCallResult(false, null, ex.Message, 
                stopwatch.Elapsed, externalCall.CanRetry(3));
        }
    }

    private async Task<ExternalCallResult> ExecuteCallByType(
        WorkflowExternalCall externalCall, 
        CancellationToken cancellationToken)
    {
        return externalCall.Type switch
        {
            ExternalCallType.HttpRequest => await ExecuteHttpRequest(externalCall, cancellationToken),
            ExternalCallType.ThirdPartyApi => await ExecuteThirdPartyApiCall(externalCall, cancellationToken),
            ExternalCallType.EmailService => await ExecuteEmailService(externalCall, cancellationToken),
            ExternalCallType.NotificationService => await ExecuteNotificationService(externalCall, cancellationToken),
            _ => throw new NotSupportedException($"External call type {externalCall.Type} is not supported")
        };
    }

    private async Task<ExternalCallResult> ExecuteHttpRequest(
        WorkflowExternalCall externalCall, 
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(
                new HttpMethod(externalCall.Method.ToUpperInvariant()),
                externalCall.Endpoint);

            // Add idempotency header
            request.Headers.Add("X-Idempotency-Key", externalCall.IdempotencyKey);

            // Add custom headers
            foreach (var header in externalCall.Headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            // Add request body if present
            if (!string.IsNullOrEmpty(externalCall.RequestPayload))
            {
                request.Content = new StringContent(
                    externalCall.RequestPayload,
                    Encoding.UTF8,
                    "application/json");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new ExternalCallResult(true, responseContent);
            }
            else
            {
                return new ExternalCallResult(false, null, 
                    $"HTTP {(int)response.StatusCode}: {responseContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            return new ExternalCallResult(false, null, ex.Message);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return new ExternalCallResult(false, null, "Request timed out");
        }
    }

    private async Task<ExternalCallResult> ExecuteThirdPartyApiCall(
        WorkflowExternalCall externalCall, 
        CancellationToken cancellationToken)
    {
        // Placeholder for third-party API calls
        // This would implement specific API client logic
        await Task.Delay(100, cancellationToken); // Simulate API call
        return new ExternalCallResult(true, JsonSerializer.Serialize(new { success = true, timestamp = DateTime.UtcNow }));
    }

    private async Task<ExternalCallResult> ExecuteEmailService(
        WorkflowExternalCall externalCall, 
        CancellationToken cancellationToken)
    {
        // Placeholder for email service integration
        await Task.Delay(50, cancellationToken); // Simulate email sending
        return new ExternalCallResult(true, JsonSerializer.Serialize(new { messageId = Guid.NewGuid(), sent = true }));
    }

    private async Task<ExternalCallResult> ExecuteNotificationService(
        WorkflowExternalCall externalCall, 
        CancellationToken cancellationToken)
    {
        // Placeholder for notification service integration
        await Task.Delay(25, cancellationToken); // Simulate notification
        return new ExternalCallResult(true, JsonSerializer.Serialize(new { notificationId = Guid.NewGuid(), delivered = true }));
    }

    public async Task<ExternalCallResult> CompleteExternalCallAsync(
        Guid externalCallId,
        string responsePayload,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var externalCall = await _context.Set<WorkflowExternalCall>()
            .FirstOrDefaultAsync(c => c.Id == externalCallId, cancellationToken);

        if (externalCall == null)
        {
            return new ExternalCallResult(false, null, "External call not found");
        }

        externalCall.MarkAsCompleted(responsePayload, duration);
        await _context.SaveChangesAsync(cancellationToken);

        return new ExternalCallResult(true, responsePayload, null, duration);
    }

    public async Task<ExternalCallResult> FailExternalCallAsync(
        Guid externalCallId,
        string errorMessage,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var externalCall = await _context.Set<WorkflowExternalCall>()
            .FirstOrDefaultAsync(c => c.Id == externalCallId, cancellationToken);

        if (externalCall == null)
        {
            return new ExternalCallResult(false, null, "External call not found");
        }

        externalCall.MarkAsFailed(errorMessage, duration);
        await _context.SaveChangesAsync(cancellationToken);

        return new ExternalCallResult(false, null, errorMessage, duration, externalCall.CanRetry(3));
    }

    public async Task<List<WorkflowExternalCall>> GetPendingExternalCallsAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<WorkflowExternalCall>()
            .Where(c => c.Status == ExternalCallStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowExternalCall>> GetRetryableExternalCallsAsync(
        int maxRetries = 3,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<WorkflowExternalCall>()
            .Where(c => (c.Status == ExternalCallStatus.Failed || c.Status == ExternalCallStatus.TimedOut) &&
                       c.AttemptCount < maxRetries)
            .OrderBy(c => c.CreatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    private string GetServiceKey(WorkflowExternalCall externalCall)
    {
        // Extract service key from endpoint for circuit breaker grouping
        try
        {
            var uri = new Uri(externalCall.Endpoint);
            return $"{externalCall.Type}-{uri.Host}";
        }
        catch
        {
            return $"{externalCall.Type}-{externalCall.Endpoint.Take(50)}";
        }
    }
}