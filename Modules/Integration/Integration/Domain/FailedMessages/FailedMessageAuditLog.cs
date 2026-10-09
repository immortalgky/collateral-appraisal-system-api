namespace Integration.Domain.FailedMessages;

/// <summary>
/// Append-only trail of every retry/discard/resend on a failed consumer message or a failed
/// outbox row (design §1/§2). Kept forever (design assumption 5).
/// </summary>
public class FailedMessageAuditLog
{
    public Guid Id { get; private set; }
    public string Action { get; private set; } = default!;
    public string Source { get; private set; } = default!;

    /// <summary><see cref="FailedMessage.Id"/> when <see cref="Source"/> is Consumer, the outbox row Id when Outbox.</summary>
    public Guid TargetId { get; private set; }

    /// <summary>Outbox module whitelist value (design api-contract.md); set only when <see cref="Source"/> is Outbox.</summary>
    public string? OutboxModule { get; private set; }

    /// <summary>Null for a server-generated entry (design D6/<see cref="FailedMessageAuditAction.RetryFailed"/>)
    /// — no signed-in user performed it; the acting node name is embedded in <see cref="Reason"/> instead.</summary>
    public string? ActorCode { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Reason { get; private set; }
    public DateTime At { get; private set; }

    private FailedMessageAuditLog()
    {
    }

    public static FailedMessageAuditLog Create(
        string action, string source, Guid targetId, string? outboxModule, string? actorCode,
        string? ipAddress, string? reason, DateTime at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return new FailedMessageAuditLog
        {
            Id = Guid.CreateVersion7(),
            Action = action,
            Source = source,
            TargetId = targetId,
            OutboxModule = outboxModule,
            ActorCode = actorCode,
            IpAddress = ipAddress,
            Reason = reason,
            At = at
        };
    }
}

public static class FailedMessageAuditAction
{
    public const string Retry = "Retry";
    public const string Discard = "Discard";
    public const string OutboxResend = "OutboxResend";

    /// <summary>Server-generated (no ActorCode from a signed-in user) — written when the collector's
    /// republish finds the original queue gone, is nacked by the broker, or can't even be built
    /// (api-contract.md, FailedMessage.RevertRetry).</summary>
    public const string RetryFailed = "RetryFailed";
}

public static class FailedMessageAuditSource
{
    public const string Consumer = "Consumer";
    public const string Outbox = "Outbox";
}
