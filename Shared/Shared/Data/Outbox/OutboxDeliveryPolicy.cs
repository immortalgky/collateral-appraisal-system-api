namespace Shared.Data.Outbox;

/// <summary>
/// Shared timing knobs for outbox delivery, kept in one place so every delivery service instance
/// agrees on the same values.
/// </summary>
public static class OutboxDeliveryPolicy
{
    /// <summary>
    /// How long a version-skew failure — an unresolvable event type, or a payload that no longer
    /// deserialises against the type it names — is treated as "not deployed on this node yet"
    /// (e.g. an old instance still holding the lease during a rolling deploy, running code that
    /// predates the type or its current shape) before it's given up on and marked <c>Failed</c>.
    /// </summary>
    public static readonly TimeSpan VersionSkewGracePeriod = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long a message held for version skew is excluded from the batch query before it's
    /// reconsidered. Keeps a message that's still within its grace period from being re-claimed
    /// (and re-held) on every single poll.
    /// </summary>
    public static readonly TimeSpan HeldRecheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Cap on how many held message ids a single delivery service instance keeps in memory at
    /// once. Without a cap, enough held messages could grow the exclusion list unbounded; when
    /// full, the soonest-due entry is dropped to make room (it's simply rechecked a little early).
    /// </summary>
    public const int MaxHeldMessages = 500;

    /// <summary>
    /// Cap on how many "transport suspect" message ids (messages that last aborted a batch with a
    /// publish timeout or a transport error) a delivery service instance keeps in memory. Same
    /// bounding style as <see cref="MaxHeldMessages"/>: when full, the oldest entry is dropped.
    /// </summary>
    public const int MaxTransportSuspects = 500;

    /// <summary>
    /// Upper bound for a save or lease-release issued during shutdown or while resetting a batch,
    /// so a blocked database call can't hold the process open past the host's own shutdown timeout.
    /// Every delivery service (one per DbContext, six at present) links ApplicationStopping into its
    /// loop, so they all cancel at the same moment and their final sweeps run concurrently rather than
    /// one after another; each can spend this twice — a sweep save plus a lease release — so the
    /// worst case with a hung database is about 2 x 10s = 20s, inside the host's 30s shutdown timeout.
    /// A shorter cap would strand already-published rows in Processing on a merely slow database.
    /// Only applied once the token is already cancelled — a healthy save on a live token keeps the
    /// old unbounded behaviour, so a merely slow (not down) database can't have a good save
    /// cancelled out from under it.
    /// </summary>
    public static readonly TimeSpan ShutdownSafeSaveTimeout = TimeSpan.FromSeconds(10);

    // ---- Retention and orphaned/stuck-Processing thresholds, kept next to the other
    // shared outbox delivery timing knobs. ----

    /// <summary>How long a Processed outbox row (and a Processed inbox entry) is kept before
    /// <see cref="OutboxCleanupJob{TDbContext}"/> deletes it. Also the horizon past which a row's
    /// newer-sent count can no longer be known, because the newer rows may already be purged.</summary>
    public const int ProcessedRetentionDays = 7;

    /// <summary>How long a Failed outbox row is kept, counted from its last claim (or from
    /// <c>OccurredAt</c> if it has none) — longer than Processed so an operator has time to resend it
    /// from the Failed Messages screen.</summary>
    public const int FailedRetentionDays = 90;

    /// <summary>
    /// Shared "stuck Processing" threshold for outbox rows. A row Processing for longer than this is
    /// treated as orphaned (crash / ungraceful shutdown) and reset to Pending, or reported as Stuck.
    /// Fixed at 5 minutes rather than a multiple of the configurable OutboxDelivery:LeaseDuration:
    /// a threshold shorter than the time a batch can legitimately still be in flight on
    /// another node risks resetting rows mid-publish and double-publishing them. Used by
    /// <c>IntegrationEventDeliveryService.ResetOrphanedProcessingAsync</c> (the only place a non-NULL
    /// Processing row is reset at this threshold — it runs under the delivery lease, which the daily
    /// <see cref="OutboxCleanupJob{TDbContext}"/> does not; the job only backstops at
    /// <see cref="StuckProcessingBackstopThreshold"/>), the Failed Messages API's Stuck filter/summary
    /// count, and the cleanup job's stale-inbox-claim delete — one constant so they all agree.
    /// </summary>
    public static readonly TimeSpan OrphanedProcessingThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Separate, shorter "Stuck" display threshold (docs/failed-messages/api-contract.md) — operators are
    /// meant to see a row as stuck on the Failed Messages screen *before* <see cref="OrphanedProcessingThreshold"/>'s
    /// 5-minute auto-reset fires, so this can never be the same constant. Used by
    /// <c>GET /admin/outbox-messages?status=Stuck</c> and the summary's <c>outboxStuckCount</c>.
    /// </summary>
    public static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A Processing row whose <c>ProcessingStartedAt</c> is NULL comes only from a
    /// node still on an old binary mid-rolling-deploy (it marks a row Processing without stamping the
    /// column) — not a crash on this row specifically. Resetting it as aggressively as
    /// <see cref="OrphanedProcessingThreshold"/>'s 5-minute rule risks resetting a row that's still
    /// legitimately in flight on that old binary. Only <see cref="OutboxCleanupJob{TDbContext}"/> resets
    /// a NULL row (keyed on <c>OccurredAt</c>, the column a NULL row still has) — the delivery service's
    /// own per-poll <c>ResetOrphanedProcessingAsync</c> ignores NULL entirely and leaves it to this job.
    /// The Stuck *display* threshold (<see cref="StuckThreshold"/>) is a third, unrelated rule — it only
    /// decides what operators SEE as stuck, not what gets reset.
    /// </summary>
    public static readonly TimeSpan NullProcessingStartedAtOrphanedThreshold = TimeSpan.FromHours(1);

    /// <summary>
    /// Backstop for <see cref="OutboxCleanupJob{TDbContext}"/>'s daily run: a Processing row whose
    /// <c>ProcessingStartedAt</c> is NON-NULL and older than this is reset to Pending even though the job ignores
    /// the lease. Normally the delivery service's own per-poll <c>ResetOrphanedProcessingAsync</c>
    /// (<see cref="OrphanedProcessingThreshold"/>, 5 minutes) does that; this only matters when that reset keeps
    /// failing (its failures are swallowed) or never runs. One hour is far beyond any possible batch — 50 messages
    /// x 15s publish timeout = 12.5 minutes, and mid-batch lease renewal keeps the lease throughout — so a live
    /// batch is never mistaken for an orphan, yet an orphaned row is not left waiting indefinitely.
    /// </summary>
    public static readonly TimeSpan StuckProcessingBackstopThreshold = TimeSpan.FromHours(1);
}
