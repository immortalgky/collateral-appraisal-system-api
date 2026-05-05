using Collateral.CollateralMasters.Events;
using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.EventHandlers;

/// <summary>
/// Handles CollateralMasterEditedEvent, CollateralMasterSoftDeletedEvent, and
/// CollateralMasterRestoredEvent by writing an audit log row to CollateralMasterAuditLogs.
///
/// Runs inside the same DispatchDomainEventInterceptor transaction — uses the same
/// CollateralDbContext that called SaveChanges, so the audit row is committed atomically.
/// </summary>
public class CollateralMasterEditedAuditLogWriter(CollateralDbContext dbContext)
    : INotificationHandler<CollateralMasterEditedEvent>
{
    public Task Handle(CollateralMasterEditedEvent notification, CancellationToken cancellationToken)
    {
        var log = new CollateralMasterAuditLog(
            collateralMasterId: notification.MasterId,
            action: "Edit",
            changedFields: notification.ChangedFields,
            reason: notification.Reason,
            changedBy: notification.By);

        dbContext.CollateralMasterAuditLogs.Add(log);
        return Task.CompletedTask;
    }
}

public class CollateralMasterSoftDeletedAuditLogWriter(CollateralDbContext dbContext)
    : INotificationHandler<CollateralMasterSoftDeletedEvent>
{
    public Task Handle(CollateralMasterSoftDeletedEvent notification, CancellationToken cancellationToken)
    {
        var log = new CollateralMasterAuditLog(
            collateralMasterId: notification.MasterId,
            action: "SoftDelete",
            changedFields: null,
            reason: notification.Reason,
            changedBy: notification.By);

        dbContext.CollateralMasterAuditLogs.Add(log);
        return Task.CompletedTask;
    }
}

public class CollateralMasterRestoredAuditLogWriter(CollateralDbContext dbContext)
    : INotificationHandler<CollateralMasterRestoredEvent>
{
    public Task Handle(CollateralMasterRestoredEvent notification, CancellationToken cancellationToken)
    {
        var log = new CollateralMasterAuditLog(
            collateralMasterId: notification.MasterId,
            action: "Restore",
            changedFields: null,
            reason: notification.Reason,
            changedBy: notification.By);

        dbContext.CollateralMasterAuditLogs.Add(log);
        return Task.CompletedTask;
    }
}
