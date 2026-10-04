namespace Integration.Domain.FailedMessages;

/// <summary>
/// One row per MassTransit message the per-node collector found in a <c>*_error</c>/<c>*_skipped</c>
/// queue (design D1/D10). Retry/discard only move a <see cref="FailedMessageStatus.Pending"/> row;
/// the actual AMQP republish is performed by the owning node's collector (design D3), which then
/// calls <see cref="MarkRetried"/>.
/// </summary>
public class FailedMessage
{
    public Guid Id { get; private set; }
    public string Node { get; private set; } = default!;
    public string SourceQueue { get; private set; } = default!;
    public string Kind { get; private set; } = default!;
    public Guid? MessageId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string MessageType { get; private set; } = default!;
    public string? ConsumerType { get; private set; }
    public string ExceptionType { get; private set; } = default!;
    public string ExceptionMessage { get; private set; } = default!;

    public string? StackTrace { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime FaultedAt { get; private set; }
    public DateTime CollectedAt { get; private set; }
    public string? RefType { get; private set; }
    public Guid? RefId { get; private set; }
    public string? RefNumber { get; private set; }
    public byte[] Body { get; private set; } = [];
    public string? ContentType { get; private set; }
    public Dictionary<string, string> Headers { get; private set; } = new();
    public string Status { get; private set; } = default!;
    public string? ActionBy { get; private set; }
    public DateTime? ActionAt { get; private set; }
    public string? ActionReason { get; private set; }

    /// <summary>
    /// Set by the collector's single conditional claim UPDATE right before
    /// it publishes a <see cref="FailedMessageStatus.RetryRequested"/> row, so a second collector sharing
    /// the same <see cref="Node"/> (an IIS overlapped recycle, or a second process) can't also publish it.
    /// Cleared on <see cref="MarkRetried"/> and <see cref="RevertRetry"/>. A claim older than
    /// <see cref="FailedMessageRetryClaimPolicy.StaleAfter"/> is stale and can be re-claimed/discarded.
    /// </summary>
    public DateTime? RetryClaimedAt { get; private set; }

    /// <summary>SQL Server rowversion concurrency token — see design §1.</summary>
    public byte[] RowVersion { get; private set; } = [];

    private FailedMessage()
    {
    }

    public static FailedMessage Create(
        string node,
        string sourceQueue,
        string kind,
        Guid? messageId,
        Guid? conversationId,
        string messageType,
        string? consumerType,
        string exceptionType,
        string exceptionMessage,
        string? stackTrace,
        int retryCount,
        DateTime faultedAt,
        DateTime collectedAt,
        string? refType,
        Guid? refId,
        string? refNumber,
        byte[] body,
        string? contentType,
        Dictionary<string, string>? headers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceQueue);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionMessage);

        return new FailedMessage
        {
            Id = Guid.CreateVersion7(),
            Node = node,
            SourceQueue = sourceQueue,
            Kind = kind,
            MessageId = messageId,
            ConversationId = conversationId,
            MessageType = messageType,
            ConsumerType = consumerType,
            ExceptionType = exceptionType,
            ExceptionMessage = exceptionMessage,
            StackTrace = stackTrace,
            RetryCount = retryCount,
            FaultedAt = faultedAt,
            CollectedAt = collectedAt,
            RefType = refType,
            RefId = refId,
            RefNumber = refNumber,
            Body = body,
            ContentType = contentType,
            Headers = headers ?? new Dictionary<string, string>(),
            Status = FailedMessageStatus.Pending
        };
    }

    /// <summary>Admin requests a retry; the owning node's collector performs the actual republish.</summary>
    public bool RequestRetry(string actorCode, string? reason, DateTime now)
    {
        if (Status != FailedMessageStatus.Pending)
            return false;

        Status = FailedMessageStatus.RetryRequested;
        ActionBy = actorCode;
        ActionAt = now;
        ActionReason = reason;
        return true;
    }

    /// <summary>
    /// Collector confirms the republish landed back on the original queue.
    /// Stamps <see cref="ActionAt"/> to NOW (when the retry actually resolved), not left at whatever
    /// <see cref="RequestRetry"/> set it to (when the retry was only requested) — the summary's
    /// <c>last24h.retried</c> and the 90-day cleanup job both key off <see cref="ActionAt"/> and are
    /// meant to mean "resolved in the window", not "requested in the window". <see cref="ActionBy"/> is
    /// left untouched — it stays the actor who requested the retry.
    /// </summary>
    public bool MarkRetried(DateTime now)
    {
        if (Status != FailedMessageStatus.RetryRequested)
            return false;

        Status = FailedMessageStatus.Retried;
        ActionAt = now;
        RetryClaimedAt = null;
        return true;
    }

    /// <summary>
    /// Collector's republish came back UNROUTABLE (the original queue no longer exists on the broker) —
    /// puts the row back to Pending with a system-generated reason instead of leaving it stuck
    /// RetryRequested forever (api-contract.md, retry `RetryFailed` history action).
    /// <see cref="ActionBy"/> is left as whatever <see cref="RequestRetry"/> set it to (the actor who
    /// asked for the retry) — this failure is reported via the separate <c>RetryFailed</c> audit row
    /// (ActorCode null there, per the contract), not by overwriting who originally requested the retry.
    /// </summary>
    public bool RevertRetry(string reason, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status != FailedMessageStatus.RetryRequested)
            return false;

        Status = FailedMessageStatus.Pending;
        ActionAt = now;
        ActionReason = reason;
        RetryClaimedAt = null;
        return true;
    }

    /// <summary>
    /// Called only on a fresh, not-yet-inserted instance, when the collector's dedup check found
    /// that this exact key collided with a row that already moved past Pending (Retried/Discarded) —
    /// i.e. this is a NEW failure reusing an old key, not a redelivery. Bumps FaultedAt to collection
    /// time so the (MessageId, SourceQueue, Kind, FaultedAt) key no longer collides.
    /// </summary>
    internal void RebaseFaultedAt(DateTime faultedAt) => FaultedAt = faultedAt;

    /// <summary>
    /// Accepts Pending (the normal case) or RetryRequested — the way out for a row stuck RetryRequested
    /// because its owning node is dead and will never pick it up (api-contract.md discard).
    /// </summary>
    public bool Discard(string actorCode, string reason, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status != FailedMessageStatus.Pending && Status != FailedMessageStatus.RetryRequested)
            return false;

        Status = FailedMessageStatus.Discarded;
        ActionBy = actorCode;
        ActionAt = now;
        ActionReason = reason;
        return true;
    }
}

public static class FailedMessageKind
{
    public const string Error = "Error";
    public const string Skipped = "Skipped";
}

public static class FailedMessageStatus
{
    public const string Pending = "Pending";
    public const string RetryRequested = "RetryRequested";
    public const string Retried = "Retried";
    public const string Discarded = "Discarded";
}

/// <summary>
/// The one constant for how long a <see cref="FailedMessage.RetryClaimedAt"/> claim is honoured —
/// shared by the collector's claim UPDATE and the discard handler's "Publishing" skip check, so they can
/// never disagree on when a claim is stale enough to re-claim or discard through.
/// </summary>
public static class FailedMessageRetryClaimPolicy
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
}
