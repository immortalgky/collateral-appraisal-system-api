using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;
using Shared.Configurations;
using Shared.Data.Outbox;
using Shared.Time;

namespace Shared.Messaging.Services;

public class IntegrationEventDeliveryService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationEventDeliveryService<TDbContext>> logger,
    IDateTimeProvider dateTimeProvider,
    IOptions<BackgroundJobsOptions> options,
    IHostApplicationLifetime lifetime)
    : BackgroundService where TDbContext : DbContext
{
    private readonly int _batchSize = options.Value.OutboxDelivery.BatchSize;
    private readonly int _maxRetries = options.Value.OutboxDelivery.MaxRetries;
    private readonly TimeSpan _pollInterval = options.Value.OutboxDelivery.PollInterval;
    private readonly TimeSpan _leaseDuration = options.Value.OutboxDelivery.LeaseDuration;
    private readonly TimeSpan _publishTimeout = options.Value.OutboxDelivery.PublishTimeout;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _lockId = typeof(TDbContext).Name;
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    // Held GROUPS for version skew — keyed the same way delivery ordering groups messages
    // (CorrelationId; a message with no CorrelationId is its own group of one, keyed by its own
    // Id instead). Two maps, not one, because the batch query below needs to exclude by
    // CorrelationId or by Id without ever calling .ToString() on a Guid inside the query (not
    // reliably translatable). Single-writer: only one ProcessBatchAsync call is ever in flight per
    // instance, so no locking is needed.
    private readonly Dictionary<string, DateTime> _heldCorrelations = new();
    private readonly Dictionary<Guid, DateTime> _heldSoloIds = new();

    // "Transport suspects": ids of messages that last aborted a batch with a publish timeout or a
    // transport error, mapped to when that happened. A timeout alone can't tell a dead broker from a
    // message that times out because of itself (e.g. a huge payload), so a suspect's whole group is
    // processed LAST next batch: if the broker works, others publish first and the suspect's next timeout
    // is provably its own fault. Bounded like the held maps (oldest entry evicted at the cap); single-writer
    // for the same reason.
    private readonly Dictionary<Guid, DateTime> _transportSuspects = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[OUTBOX] Delivery service started for {DbContext} (instance: {InstanceId})",
            _lockId, _instanceId);

        // MassTransit is registered BEFORE these delivery services in Program.cs: hosted services
        // start in registration order and stop in reverse, so the bus starts first and stops after
        // them. ApplicationStopping is still linked in here as defence in depth: if that order is ever
        // disturbed, a publish made after the bus stops fails with "bus stopping" rather than
        // OperationCanceledException, which stoppingToken alone would not catch. One linked token
        // covers both checks everywhere below.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, lifetime.ApplicationStopping);
        var token = cts.Token;

        try
        {
            while (!token.IsCancellationRequested)
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

                    // Captured BEFORE the acquire so the renewal baseline never understates the lease's age.
                    var leaseStampedAt = dateTimeProvider.ApplicationNow;
                    if (!await TryAcquireLeaseAsync(dbContext, token))
                    {
                        await Task.Delay(_pollInterval, token);
                        continue;
                    }

                    var bus = scope.ServiceProvider.GetRequiredService<IBus>();
                    var processedCount = await ProcessBatchAsync(dbContext, bus, token, leaseStampedAt: leaseStampedAt);

                    if (processedCount == 0)
                        await Task.Delay(_pollInterval, token);
                }
                catch (Exception ex) when (token.IsCancellationRequested)
                {
                    // A plain OperationCanceledException is expected shutdown noise. Anything else
                    // (a real bug that happened to fire while shutting down) must still be visible.
                    if (ex is OperationCanceledException)
                        logger.LogInformation("[OUTBOX] Shutdown requested; stopping delivery loop for {DbContext}",
                            _lockId);
                    else
                        logger.LogWarning(ex, "[OUTBOX] Non-cancellation error while shutting down for {DbContext}",
                            _lockId);
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[OUTBOX] Error in delivery service for {DbContext}", _lockId);
                    try
                    {
                        await Task.Delay(_pollInterval, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
        }
        finally
        {
            // Release the lease however the loop above exits, so another instance doesn't wait
            // out the full lease duration for a clean shutdown. Bounded so a blocked database
            // can't hold the process open past the host's own shutdown timeout.
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                using var releaseCts = new CancellationTokenSource(OutboxDeliveryPolicy.ShutdownSafeSaveTimeout);
                await ReleaseLeaseAsync(dbContext, releaseCts.Token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[OUTBOX] Failed to release lease on shutdown for {DbContext}", _lockId);
            }
        }

        logger.LogInformation("[OUTBOX] Delivery service stopped for {DbContext}", _lockId);
    }

    private async Task<bool> TryAcquireLeaseAsync(TDbContext dbContext, CancellationToken ct)
    {
        var now = dateTimeProvider.ApplicationNow;
        var leasedUntil = now.Add(_leaseDuration);
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        // Try to renew existing lease or claim an expired one
        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[BackgroundServiceLease] " +
            "SET InstanceId = {0}, LeasedUntil = {1}, AcquiredAt = {2} " +
            "WHERE Id = {3} AND (InstanceId = {0} OR LeasedUntil < {2})",
            new object[] { _instanceId, leasedUntil, now, _lockId }, ct);

        if (rowsAffected > 0)
            return true;

        // Try to insert if no row exists (first time)
        try
        {
            var inserted = await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO [" + schema + "].[BackgroundServiceLease] (Id, InstanceId, LeasedUntil, AcquiredAt) " +
                "SELECT {0}, {1}, {2}, {3} " +
                "WHERE NOT EXISTS (SELECT 1 FROM [" + schema + "].[BackgroundServiceLease] WHERE Id = {0})",
                new object[] { _lockId, _instanceId, leasedUntil, now }, ct);

            // 0 rows = a non-expired lease row already exists, owned by another instance
            return inserted > 0;
        }
        catch (DbUpdateException)
        {
            // Concurrent first-time INSERT race — PK violation, lost the race
            return false;
        }
    }

    /// <summary>
    /// Mid-batch renewal. Unlike <see cref="TryAcquireLeaseAsync"/> this does NOT take over an
    /// expired lease: it only extends the row while <c>InstanceId</c> is still ours. Acquiring
    /// always rewrites <c>InstanceId</c>, so if it is still ours nobody else has held the lease in
    /// between — even if it expired — and zero rows means ownership was lost.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The only concatenated fragment is the schema name, read from the EF model (GetDefaultSchema) — " +
            "never caller input. Every value (lease expiry, timestamp, lock id, instance id) is bound as a " +
            "positional {n} parameter.")]
    private async Task<bool> RenewLeaseAsync(TDbContext dbContext, CancellationToken ct)
    {
        var now = dateTimeProvider.ApplicationNow;
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[BackgroundServiceLease] " +
            "SET LeasedUntil = {0}, AcquiredAt = {1} " +
            "WHERE Id = {2} AND InstanceId = {3}",
            new object[] { now.Add(_leaseDuration), now, _lockId, _instanceId }, ct);

        return rowsAffected > 0;
    }

    private async Task ReleaseLeaseAsync(TDbContext dbContext, CancellationToken ct)
    {
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[BackgroundServiceLease] " +
            "SET LeasedUntil = {0} " +
            "WHERE Id = {1} AND InstanceId = {2}",
            new object[] { dateTimeProvider.ApplicationNow, _lockId, _instanceId }, ct);
    }

    /// <summary>
    /// A healthy save on a live token keeps the old, unbounded behaviour (CancellationToken.None)
    /// — a merely slow database must not have a good save cancelled out from under it. Only once the
    /// token is already cancelled does the save get a bounded timeout, so it can't outlive the host's
    /// own shutdown window.
    /// </summary>
    private static Task SaveChangesBoundedAsync(TDbContext dbContext, CancellationToken token)
    {
        if (!token.IsCancellationRequested)
            return dbContext.SaveChangesAsync(CancellationToken.None);

        return SaveWithTimeoutAsync(dbContext);

        static async Task SaveWithTimeoutAsync(TDbContext dbContext)
        {
            using var cts = new CancellationTokenSource(OutboxDeliveryPolicy.ShutdownSafeSaveTimeout);
            await dbContext.SaveChangesAsync(cts.Token);
        }
    }

    // Channel-level soft errors from RabbitMQ (bad routing/exchange arguments, access refused,
    // etc.) — a problem with THIS message, not the broker. An OperationInterruptedException carrying one
    // of these must burn a retry like any other publish failure, not be retried forever as if the
    // broker were down. Verified these exist with these exact values in RabbitMQ.Client 7.1.2 via
    // reflection over RabbitMQ.Client.Constants — see the PR notes for the exact command.
    private static readonly ushort[] ChannelLevelSoftErrorReplyCodes =
    [
        RabbitMQ.Client.Constants.AccessRefused, // 403
        RabbitMQ.Client.Constants.NotFound, // 404
        RabbitMQ.Client.Constants.ResourceLocked, // 405
        RabbitMQ.Client.Constants.PreconditionFailed // 406
    ];

    /// <summary>
    /// Looks for a sign that the broker itself is unreachable, as opposed to a problem with this one
    /// message. Verified against the installed MassTransit 8.4.1 / RabbitMQ.Client 7.1.2 assemblies by
    /// reflection — see the PR notes for the exact command. <see cref="ConnectionException"/> covers
    /// <c>RabbitMqConnectionException</c> (its only subclass at this version) so future transports that
    /// also derive from it are covered for free.
    /// The whole InnerException chain is scanned for a channel soft-error close FIRST: MassTransit wraps
    /// such a close in a <c>RabbitMqConnectionException</c>, so judging the outermost type alone would
    /// call a poison message "transport unavailable" and it would never burn a retry.
    /// </summary>
    private static bool IsTransportUnavailable(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            // The BASE class: the first failed channel RPC throws OperationInterruptedException itself,
            // later calls on the closed channel throw its subclass AlreadyClosedException (verified in
            // RabbitMQ.Client 7.1.2 by reflection). ShutdownReason is declared on the base.
            if (current is OperationInterruptedException { ShutdownReason.ReplyCode: var replyCode }
                && ChannelLevelSoftErrorReplyCodes.Contains(replyCode))
                return false; // this message's fault, not the broker's
        }

        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is AlreadyClosedException or ConnectionException or TransportUnavailableException
                or BrokerUnreachableException or ConnectFailureException)
                return true;
        }

        return false;
    }

    private void PruneHeld(DateTime now)
    {
        foreach (var expired in _heldCorrelations.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
            _heldCorrelations.Remove(expired);
        foreach (var expired in _heldSoloIds.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
            _heldSoloIds.Remove(expired);
    }

    private void MarkTransportSuspect(Guid messageId, DateTime now)
    {
        if (!_transportSuspects.ContainsKey(messageId)
            && _transportSuspects.Count >= OutboxDeliveryPolicy.MaxTransportSuspects)
            _transportSuspects.Remove(_transportSuspects.MinBy(kv => kv.Value).Key);
        _transportSuspects[messageId] = now;
    }

    private int HeldCount => _heldCorrelations.Count + _heldSoloIds.Count;

    private void EvictSoonestDueIfAtCapacity()
    {
        if (HeldCount < OutboxDeliveryPolicy.MaxHeldMessages)
            return;

        // Cap reached — drop the soonest-due entry (whichever map it's in) to make room. It's
        // simply rechecked a little early next batch rather than the new hold being dropped.
        var soonestCorrelation = _heldCorrelations.Count > 0
            ? _heldCorrelations.MinBy(kv => kv.Value)
            : (KeyValuePair<string, DateTime>?)null;
        var soonestSolo = _heldSoloIds.Count > 0
            ? _heldSoloIds.MinBy(kv => kv.Value)
            : (KeyValuePair<Guid, DateTime>?)null;

        if (soonestCorrelation is { } correlation && (soonestSolo is not { } solo || correlation.Value <= solo.Value))
            _heldCorrelations.Remove(correlation.Key);
        else if (soonestSolo is { } solo2)
            _heldSoloIds.Remove(solo2.Key);
    }

    /// <summary>
    /// Holds the message's whole GROUP (its CorrelationId, or its own Id when it has none) back from
    /// the next batch(es) until <see cref="OutboxDeliveryPolicy.HeldRecheckInterval"/> passes — not
    /// just this one message — so a later message in the same correlation can never overtake the one
    /// being held, and the group is released together (head included) whenever it expires or is
    /// evicted. Removal relies entirely on due-time expiry (<see cref="PruneHeld"/>) — no explicit
    /// removal when a message leaves Pending some other way, because a held group only ever leaves
    /// Pending via a path that doesn't hold it (resolved-and-delivered, or past-grace-period
    /// MarkAsFailed on its head), so by construction nothing needs to reach in and remove it early.
    /// </summary>
    private void Hold(IntegrationEventOutboxMessage message, DateTime now)
    {
        var dueAt = now.Add(OutboxDeliveryPolicy.HeldRecheckInterval);

        if (message.CorrelationId is { } correlationId)
        {
            if (!_heldCorrelations.ContainsKey(correlationId))
                EvictSoonestDueIfAtCapacity();
            _heldCorrelations[correlationId] = dueAt;
        }
        else
        {
            if (!_heldSoloIds.ContainsKey(message.Id))
                EvictSoonestDueIfAtCapacity();
            _heldSoloIds[message.Id] = dueAt;
        }
    }

    /// <summary>
    /// <c>renewLease</c> defaults to <see cref="RenewLeaseAsync"/>, which only succeeds while
    /// this instance is still the recorded owner. Overridable so a test can simulate a renewal
    /// succeeding or failing without going through the raw SQL (unsupported on the InMemory
    /// provider used in tests).
    /// </summary>
    internal async Task<int> ProcessBatchAsync(
        TDbContext dbContext,
        IBus bus,
        CancellationToken token,
        Func<TDbContext, CancellationToken, Task<bool>>? renewLease = null,
        DateTime? leaseStampedAt = null)
    {
        // Don't claim a batch we won't be able to work — leaves it for the instance that survives.
        if (token.IsCancellationRequested)
            return 0;

        renewLease ??= RenewLeaseAsync;

        var now = dateTimeProvider.ApplicationNow;
        // Mid-batch renewal measures elapsed time from when the lease was actually stamped (taken by
        // the caller just before the acquire), not from the start of this method — anything that ran
        // in between would otherwise be missing from the lease's age. Defaults to "now" for callers
        // that have no earlier stamp.
        var lastLeaseRenewal = leaseStampedAt ?? now;
        PruneHeld(now);
        var heldCorrelationIds = _heldCorrelations.Keys.ToList();
        var heldSoloIds = _heldSoloIds.Keys.ToList();

        // Excludes a whole held GROUP, not just its head: a message with a held CorrelationId is
        // excluded regardless of which row within that group triggered the hold, and a message with
        // no CorrelationId is excluded only by its own Id (it's a group of one). No .ToString() on
        // a Guid inside the query — both Contains() calls are on their own column's native type, so
        // this is fully translatable (not client-evaluated).
        var messages = await dbContext.Set<IntegrationEventOutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending
                && !(m.CorrelationId != null && heldCorrelationIds.Contains(m.CorrelationId))
                && !(m.CorrelationId == null && heldSoloIds.Contains(m.Id)))
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Take(_batchSize)
            .ToListAsync(token);

        if (messages.Count == 0)
            return 0;

        // Mark all as Processing first — prevents another instance from picking them up if our
        // lease expires during a long batch.
        foreach (var msg in messages)
            msg.MarkAsProcessing();
        await SaveChangesBoundedAsync(dbContext, token);

        // Group by CorrelationId for ordered delivery within correlation. Groups holding a transport
        // suspect go last (OrderBy is stable: every group stays intact and in order, only the order of
        // independent groups changes).
        var groups = messages.GroupBy(m => m.CorrelationId ?? m.Id.ToString())
            .OrderBy(g => g.Any(m => _transportSuspects.ContainsKey(m.Id)))
            .ToList();
        var processedCount = 0;
        var abortBatch = false;
        var completedNormally = false;

        try
        {
            foreach (var group in groups)
            {
                if (abortBatch)
                    break;

                var orderedGroup = group.OrderBy(m => m.OccurredAt).ThenBy(m => m.Id).ToList();

                for (var i = 0; i < orderedGroup.Count; i++)
                {
                    var message = orderedGroup[i];
                    var resolution = IntegrationEventNamespace.TryResolve(message.EventType, out var eventType);

                    if (resolution == TypeResolution.Unresolvable)
                    {
                        // During a rolling deploy, an old node still holding the lease may not yet
                        // know about a type only the new build defines — give it a grace period
                        // before giving up, so it isn't failed permanently just for running old code.
                        var held = HoldOrFail(message, now,
                            age => logger.LogWarning(
                                "[OUTBOX] Type {EventType} not resolvable yet for message {MessageId} (age {Age}); " +
                                "holding within grace period",
                                message.EventType, message.Id, age),
                            () => logger.LogError(
                                "[OUTBOX] Unresolvable type {EventType} for message {MessageId} past the grace period",
                                message.EventType, message.Id),
                            $"{OutboxFailureReasons.Unresolvable} {message.EventType}");

                        if (held)
                            break;
                        continue;
                    }

                    if (resolution == TypeResolution.Disallowed)
                    {
                        // Resolved but disallowed is deterministic — no grace period, no retries burned.
                        logger.LogError("[OUTBOX] Disallowed type {EventType} for message {MessageId}",
                            message.EventType, message.Id);
                        message.MarkAsFailed($"{OutboxFailureReasons.Disallowed} {message.EventType}");
                        continue;
                    }

                    object? eventObject;
                    Exception? deserializeException = null;
                    try
                    {
                        eventObject = JsonSerializer.Deserialize(message.Payload, eventType!, SerializerOptions);
                    }
                    catch (Exception ex)
                    {
                        eventObject = null;
                        deserializeException = ex;
                    }

                    if (eventObject == null)
                    {
                        // A property's shape can change mid-deploy just as easily as a whole type
                        // appearing — treat a bad payload the same as an unresolvable type, with the
                        // same grace period, instead of assuming it's always corrupt data.
                        var held = HoldOrFail(message, now,
                            age => logger.LogWarning(deserializeException,
                                "[OUTBOX] Failed to deserialize message {MessageId} (age {Age}); holding within grace period",
                                message.Id, age),
                            () => logger.LogError(deserializeException,
                                "[OUTBOX] Failed to deserialize message {MessageId} past the grace period", message.Id),
                            deserializeException == null
                                ? OutboxFailureReasons.DeserializationReturnedNull
                                : $"{OutboxFailureReasons.DeserializationFailed} {deserializeException.Message}");

                        if (held)
                            break;
                        continue;
                    }

                    // Renew the lease before each publish once a third of it has elapsed since
                    // the last (re)acquire — PublishTimeout alone (bounded above LeaseDuration by
                    // Validate()) isn't enough on its own: a batch of many merely-slow publishes can
                    // still add up past the lease without any single one timing out. Without this,
                    // another instance can claim the same rows as orphaned mid-batch and republish
                    // (or reorder) them.
                    if (dateTimeProvider.ApplicationNow - lastLeaseRenewal > _leaseDuration / 3)
                    {
                        bool renewed;
                        try
                        {
                            renewed = await renewLease(dbContext, token);
                        }
                        catch (Exception ex) when (!token.IsCancellationRequested)
                        {
                            // A renewal DB failure is indistinguishable from actually losing the
                            // lease — either way we can no longer be sure we still own these rows.
                            logger.LogWarning(ex,
                                "[OUTBOX] Failed to renew lease mid-batch for {DbContext}; aborting batch " +
                                "without burning a retry", _lockId);
                            abortBatch = true;
                            break;
                        }

                        if (!renewed)
                        {
                            // Another instance now owns the lease — stop touching these rows. No
                            // retry burned; everything untried (including this message) goes back
                            // to Pending via the sweep below for whoever holds the lease now.
                            logger.LogWarning(
                                "[OUTBOX] Lost lease mid-batch for {DbContext}; another instance owns it now — " +
                                "aborting batch without burning a retry", _lockId);
                            abortBatch = true;
                            break;
                        }

                        lastLeaseRenewal = dateTimeProvider.ApplicationNow;
                    }

                    // Bounded so a broker that accepts the connection but never acknowledges can't
                    // block this loop indefinitely — see BackgroundJobsOptions.OutboxDelivery.PublishTimeout.
                    // Linked from `token`, so a real shutdown still cancels this immediately either way.
                    using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    publishCts.CancelAfter(_publishTimeout);

                    try
                    {
                        await bus.Publish(eventObject, eventType!, publishCts.Token);
                        message.MarkAsProcessed(dateTimeProvider.ApplicationNow);
                        processedCount++;
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested
                        && (IsTransportUnavailable(ex) || publishCts.IsCancellationRequested))
                    {
                        // Either the broker is unreachable (a clear transport-down exception), or our own
                        // publish timeout fired (token itself is NOT cancelled, so this isn't shutdown —
                        // the broker just never acknowledged). Either way the whole batch aborts and the
                        // message becomes a suspect, so its group goes last next batch (which is what lets
                        // a poison message eventually meet a working broker).
                        //
                        // Only the TIMEOUT can burn a retry, and only if another publish already succeeded
                        // in this batch (the broker is evidently working, so this message is at fault).
                        // A transport-down exception never burns one: a broker that flaps after some
                        // publishes succeeded would otherwise blame the healthy in-flight message.
                        MarkTransportSuspect(message.Id, now);
                        if (!IsTransportUnavailable(ex) && processedCount > 0)
                        {
                            logger.LogWarning(ex,
                                "[OUTBOX] Message {MessageId} timed out while {Delivered} other message(s) were " +
                                "delivered in this batch (retry {RetryCount}); aborting batch",
                                message.Id, processedCount, message.RetryCount + 1);
                            message.IncrementRetryCount(
                                "Publish timed out while other messages were delivered in the same batch: " +
                                ex.Message, _maxRetries);
                        }
                        else
                        {
                            logger.LogWarning(ex,
                                "[OUTBOX] Transport unavailable or publish timed out while delivering message " +
                                "{MessageId}; aborting batch without burning a retry", message.Id);
                        }

                        abortBatch = true;
                        break;
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        // Some other publish failure — stop the whole batch, not just this
                        // correlation group, since we can no longer trust the transport is healthy.
                        // Only the message that actually failed burns a retry; everything untried
                        // goes back to Pending via the sweep below with no retry spent. Accepted
                        // cost: a poison message stops the whole batch for up to
                        // MaxRetries x PollInterval before it goes Failed and the batch moves on.
                        logger.LogWarning(ex,
                            "[OUTBOX] Failed to deliver message {MessageId} (retry {RetryCount}); aborting batch",
                            message.Id, message.RetryCount + 1);
                        message.IncrementRetryCount(ex.Message, _maxRetries);
                        abortBatch = true;
                        break;
                    }
                    // A cancelled token surfaces here as OperationCanceledException, or as a
                    // MassTransit/bus exception (e.g. "bus stopping", ObjectDisposedException) —
                    // either way it isn't caught above, so it propagates to the sweep in `finally`.
                }
            }

            completedNormally = true;
        }
        finally
        {
            // Whatever stopped the loop above — shutdown, a publish exception, or normal
            // completion — no row may leave this method still Processing.
            foreach (var msg in messages.Where(m => m.Status == OutboxMessageStatus.Processing))
                msg.MarkAsPending();

            // A suspect that was delivered or left Pending (Failed) is no longer one.
            foreach (var msg in messages.Where(m => m.Status != OutboxMessageStatus.Pending))
                _transportSuspects.Remove(msg.Id);

            try
            {
                await SaveChangesBoundedAsync(dbContext, token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[OUTBOX] Failed to save batch of {Count} messages for {DbContext}",
                    messages.Count, _lockId);

                // If the try block above already threw, that exception is what should surface —
                // rethrowing here would silently replace and hide it. Only rethrow the save
                // failure itself when nothing else was already propagating.
                if (completedNormally)
                    throw;
            }
        }

        if (processedCount > 0)
            logger.LogInformation("[OUTBOX] Delivered {Count} messages from {DbContext}",
                processedCount, _lockId);

        return processedCount;
    }

    /// <summary>
    /// Shared control flow for a version-skew failure (unresolvable type, or an undeserialisable
    /// payload): within <see cref="OutboxDeliveryPolicy.VersionSkewGracePeriod"/> of
    /// <paramref name="message"/>'s <c>OccurredAt</c>, holds its group and returns <c>true</c> (the
    /// caller must <c>break</c> out of the group's loop); past the grace period, marks it
    /// <c>Failed</c> and returns <c>false</c> (the caller continues with the rest of the group).
    /// <see cref="Hold"/> already keys on the group (CorrelationId, or the message's own Id when it
    /// has none), so holding this one message holds every sibling that shares the same key — no
    /// separate "hold the rest of the group" loop needed. Each call site supplies its own structured
    /// log calls so the two failure kinds keep their own log fields (EventType vs none) rather than
    /// forcing a shared, blander template.
    /// </summary>
    private bool HoldOrFail(
        IntegrationEventOutboxMessage message,
        DateTime now,
        Action<TimeSpan> logHoldWithinGracePeriod,
        Action logFailPastGracePeriod,
        string failureReason)
    {
        var age = now - message.OccurredAt;
        if (age < OutboxDeliveryPolicy.VersionSkewGracePeriod)
        {
            logHoldWithinGracePeriod(age);
            Hold(message, now);
            return true;
        }

        logFailPastGracePeriod();
        message.MarkAsFailed(failureReason);
        return false;
    }
}
