using Microsoft.Extensions.Configuration;
using Shared.DDD;

namespace Shared.Data.Models;

public class OutboxMessage : Entity<Guid>
{
    public DateTime OccurredOn { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public string EventType { get; private set; } = default!;
    public string? ExceptionInfo { get; private set; } = default!;
    public short RetryCount { get; private set; } = 0;
    public bool ProcessingFailed { get; private set; } = false;

    private readonly short _maxRetry = 3;

    private OutboxMessage() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private OutboxMessage(
            Guid id,
            DateTime occurredOn,
            string payload,
            string eventType,
            string? exceptionInfo
        )
    {
        Id = id;
        OccurredOn = occurredOn;
        Payload = payload;
        EventType = eventType;
        ExceptionInfo = exceptionInfo;
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
    
    public void Update(string exceptionInfo)
    {
        ExceptionInfo = exceptionInfo;
    }
    
    public void IncrementRetry()
    {
        if (RetryCount == _maxRetry)
        {
            ProcessingFailed = true;
        }
        RetryCount++;
    }
}