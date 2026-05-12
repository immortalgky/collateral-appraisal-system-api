namespace Shared.Data.Lease;

/// <summary>
/// Database-backed lease used by background services that must run on at most one
/// instance at a time across multiple servers. The holder of a row for a given
/// <see cref="Id"/> is the active leader; standby instances see <see cref="LeasedUntil"/>
/// in the future and wait.
/// </summary>
public class BackgroundServiceLease
{
    public string Id { get; private set; } = default!;
    public string InstanceId { get; private set; } = default!;
    public DateTime LeasedUntil { get; private set; }
    public DateTime AcquiredAt { get; private set; }

    private BackgroundServiceLease() { }
}
