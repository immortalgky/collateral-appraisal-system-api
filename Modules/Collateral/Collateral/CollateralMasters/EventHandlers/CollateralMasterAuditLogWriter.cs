using Collateral.CollateralMasters.Events;
using Collateral.CollateralMasters.Models;
using Shared.Time;

namespace Collateral.CollateralMasters.EventHandlers;

/// <summary>
/// Handles CollateralMasterEditedEvent, CollateralMasterSoftDeletedEvent, and
/// CollateralMasterRestoredEvent by writing an audit log row to CollateralMasterAuditLogs.
///
/// Runs inside the same DispatchDomainEventInterceptor transaction — uses the same
/// CollateralDbContext that called SaveChanges, so the audit row is committed atomically.
/// </summary>
public class CollateralMasterEditedAuditLogWriter(CollateralDbContext dbContext, IDateTimeProvider dateTimeProvider)
    : INotificationHandler<CollateralMasterEditedEvent>
{
    public Task Handle(CollateralMasterEditedEvent notification, CancellationToken cancellationToken)
    {
        var log = new CollateralMasterAuditLog(
            collateralMasterId: notification.MasterId,
            action: "Edit",
            changedFields: notification.ChangedFields,
            reason: notification.Reason,
            changedBy: notification.By,
            changedAt: dateTimeProvider.ApplicationNow);

        dbContext.CollateralMasterAuditLogs.Add(log);
        return Task.CompletedTask;
    }
}

public class CollateralMasterSoftDeletedAuditLogWriter(CollateralDbContext dbContext, IDateTimeProvider dateTimeProvider)
    : INotificationHandler<CollateralMasterSoftDeletedEvent>
{
    public Task Handle(CollateralMasterSoftDeletedEvent notification, CancellationToken cancellationToken)
    {
        var log = new CollateralMasterAuditLog(
            collateralMasterId: notification.MasterId,
            action: "SoftDelete",
            changedFields: null,
            reason: notification.Reason,
            changedBy: notification.By,
            changedAt: dateTimeProvider.ApplicationNow);

        dbContext.CollateralMasterAuditLogs.Add(log);
        return Task.CompletedTask;
    }
}

public class CollateralMasterRestoredAuditLogWriter(CollateralDbContext dbContext, IDateTimeProvider dateTimeProvider)
    : INotificationHandler<CollateralMasterRestoredEvent>
{
    public Task Handle(CollateralMasterRestoredEvent notification, CancellationToken cancellationToken)
    {
        var log = new CollateralMasterAuditLog(
            collateralMasterId: notification.MasterId,
            action: "Restore",
            changedFields: null,
            reason: notification.Reason,
            changedBy: notification.By,
            changedAt: dateTimeProvider.ApplicationNow);

        dbContext.CollateralMasterAuditLogs.Add(log);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Records the L→LB (and similar) type-upgrade transition on the master in the audit log.
/// Source change is system-driven (next appraisal flips the discriminator via LATEST-wins),
/// not user-driven — so Reason and ChangedBy are stamped as "system".
/// </summary>
public class CollateralTypeChangedAuditLogWriter(CollateralDbContext dbContext, IDateTimeProvider dateTimeProvider)
    : INotificationHandler<CollateralTypeChangedEvent>
{
    public Task Handle(CollateralTypeChangedEvent notification, CancellationToken cancellationToken)
    {
        var changedFields = System.Text.Json.JsonSerializer.Serialize(new
        {
            CollateralType = new
            {
                from = notification.OldCollateralType,
                to = notification.NewCollateralType,
            }
        });

        var log = new CollateralMasterAuditLog(
            collateralMasterId: notification.MasterId,
            action: "CollateralTypeChanged",
            changedFields: changedFields,
            reason: "LATEST-wins discriminator upgrade from new appraisal.",
            changedBy: "system",
            changedAt: dateTimeProvider.ApplicationNow);

        dbContext.CollateralMasterAuditLogs.Add(log);
        return Task.CompletedTask;
    }
}
