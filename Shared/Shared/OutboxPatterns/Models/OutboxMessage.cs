using System.Diagnostics;
using Shared.DDD;

namespace Shared.OutboxPatterns.Models;

public class OutboxMessage : Entity<Guid>
{
    public DateTime OccurredOn { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public string EventType { get; private set; } = default!;
    public string? ExceptionInfo { get; private set; } = default!;
    public bool Processed { get; private set; } = false;
    public int RetryCount { get; private set; } = 0;
    public DateTime? LastRetryAt { get; private set; }
    public int MaxRetries { get; private set; } = 3;
    public bool IsInfrastructureFailure { get; private set; } = false;

    private OutboxMessage() { }

    private OutboxMessage(
        Guid id,
        DateTime occurredOn,
        string payload,
        string eventType,
        string? exceptionInfo,
        int retryCount = 0,
        DateTime? lastRetryAt = null,
        int maxRetries = 3,
        bool isInfrastructureFailure = false
    )
    {
        Id = id;
        OccurredOn = occurredOn;
        Payload = payload;
        EventType = eventType;
        ExceptionInfo = exceptionInfo;
        RetryCount = retryCount;
        LastRetryAt = lastRetryAt;
        MaxRetries = maxRetries;
        IsInfrastructureFailure = isInfrastructureFailure;
    }

    public static OutboxMessage Create(
        Guid id,
        DateTime occurredOn,
        string payload,
        string eventType
    )
    {
        return new OutboxMessage(
            id,
            occurredOn,
            payload,
            eventType,
            null // Default ExceptionInfo to null
        );
    }

    public bool ShouldRetry()
    {
        if (IsInfrastructureFailure)
        {
            var timeSinceOccurred = DateTime.UtcNow - OccurredOn;
            return timeSinceOccurred.TotalHours < 24;
        }
        return RetryCount < MaxRetries;
    }

    public void IncrementRetry(string errorMessage, bool isInfrastructureError = false)
    {
        RetryCount++;
        LastRetryAt = DateTime.UtcNow;
        IsInfrastructureFailure = isInfrastructureError;

        var errorType = isInfrastructureError ? "INFRA" : "BUSINESS";
        ExceptionInfo = $"[{errorType}] Retry {RetryCount}: {errorMessage}";
    }

    private static bool IsInfrastructureException(Exception ex)
    {
        // ตรวจสอบประเภท exception
        var exceptionType = ex.GetType().Name.ToLower();
        var message = ex.Message.ToLower();

        return exceptionType.Contains("timeout") ||
               exceptionType.Contains("connection") ||
               exceptionType.Contains("socket") ||
               exceptionType.Contains("network") ||
               message.Contains("connection refused") ||
               message.Contains("timeout") ||
               message.Contains("connection reset") ||
               message.Contains("broker") ||
               message.Contains("rabbitmq") ||
               message.Contains("masstransit");
    }

    private static bool IsBusinessLogicException(Exception ex)
    {
        // ตรวจสอบ Business Logic errors ที่ควร limit retry
        var exceptionType = ex.GetType().Name.ToLower();
        var message = ex.Message.ToLower();

        return exceptionType.Contains("jsonserializationexception") ||
               exceptionType.Contains("jsonreaderexception") ||
               exceptionType.Contains("argumentexception") ||
               exceptionType.Contains("formatexception") ||
               exceptionType.Contains("invalidoperationexception") ||
               exceptionType.Contains("notsupportedexception") ||
               message.Contains("invalid json") ||
               message.Contains("deserialization") ||
               message.Contains("serialization") ||
               message.Contains("invalid format") ||
               message.Contains("schema validation") ||
               message.Contains("event type") ||
               message.Contains("payload validation") ||
               message.Contains("business rule");
    }

    public static bool ShouldTreatAsInfrastructureFailure(Exception ex)
    {
        if (IsBusinessLogicException(ex))
            return false;

        return IsInfrastructureException(ex);
    }
}