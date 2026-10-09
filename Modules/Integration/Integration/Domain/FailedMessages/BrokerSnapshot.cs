namespace Integration.Domain.FailedMessages;

/// <summary>
/// Latest per-node queue-health snapshot (design D9/§1) — overwritten wholesale every collector
/// round rather than kept as history. PK is <see cref="Node"/> itself: one row per app node.
/// </summary>
public class BrokerSnapshot
{
    public string Node { get; private set; } = default!;
    public DateTime CollectedAt { get; private set; }
    public string ManagementStatus { get; private set; } = default!;

    /// <summary>Per-queue detail (name, ready, unacked, consumers, rates, samples) — see api-contract.md.</summary>
    public string QueuesJson { get; private set; } = default!;

    public string? LastError { get; private set; }

    private BrokerSnapshot()
    {
    }

    public static BrokerSnapshot Create(
        string node, DateTime collectedAt, string managementStatus, string queuesJson, string? lastError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(managementStatus);

        return new BrokerSnapshot
        {
            Node = node,
            CollectedAt = collectedAt,
            ManagementStatus = managementStatus,
            QueuesJson = queuesJson ?? "[]",
            LastError = lastError
        };
    }

    /// <summary>Overwrites this node's snapshot with the latest collector round.</summary>
    public void Refresh(DateTime collectedAt, string managementStatus, string queuesJson, string? lastError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managementStatus);

        CollectedAt = collectedAt;
        ManagementStatus = managementStatus;
        QueuesJson = queuesJson ?? "[]";
        LastError = lastError;
    }
}

public static class BrokerManagementStatus
{
    public const string Ok = "Ok";
    public const string Unauthorized = "Unauthorized";
    public const string Unreachable = "Unreachable";
}
