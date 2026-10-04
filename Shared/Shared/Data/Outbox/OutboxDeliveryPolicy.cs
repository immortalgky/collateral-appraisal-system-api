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
}
