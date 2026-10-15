using Shared.CQRS;

namespace Integration.Application.Features.FailedMessages.GetFailedMessagesSummary;

public record GetFailedMessagesSummaryQuery : IQuery<FailedMessagesSummaryDto>;

public record FailedMessagesSummaryDto(
    DateTime ServerTime,
    int PendingCount,
    DateTime? OldestPendingAt,
    Last24hDto Last24h,
    IReadOnlyList<TopGroupDto> TopGroups,
    int OutboxFailedCount,
    int OutboxStuckCount,
    IReadOnlyList<NodeSnapshotDto> Nodes
);

public record Last24hDto(int Total, int Retried, int Discarded);

public record TopGroupDto(string Queue, string ExceptionType, int Count);

// CollectedAt/ManagementStatus are nullable — both omitted on the wire for a
// "ghost" node that has Pending FailedMessages rows but has never written a BrokerSnapshot row (its
// collector hasn't completed a Snapshot round yet, or the row was lost). FE shows "no snapshot yet".
public record NodeSnapshotDto(
    string Node,
    DateTime? CollectedAt,
    string? ManagementStatus,
    string? LastError,
    IReadOnlyList<QueueHealthDto> Queues
);

// Ready/Unacked/Consumers/PublishRate/DeliverRate are nullable: a node emits a queue entry for any
// base queue that has Pending FailedMessages rows (RetryRequested doesn't count) even when that
// queue is missing from the latest broker snapshot (e.g. ManagementStatus = Unauthorized, or the node
// has no BrokerSnapshot row at all yet), in which case these five are omitted and Samples is empty.
public record QueueHealthDto(
    string Name,
    int? Ready,
    int? Unacked,
    int? Consumers,
    double? PublishRate,
    double? DeliverRate,
    IReadOnlyList<int> Samples,
    int ErrorCount,
    int SkippedCount,
    bool IsOrdered
);
