using Appraisal.Infrastructure;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Persists an <see cref="AppraisalPropertyCorrectedEvent"/> as a row in
/// appraisal.AppraisalPropertyCorrectionLogs.
///
/// Runs inside the same DispatchDomainEventInterceptor transaction — it uses the same
/// AppraisalDbContext that called SaveChanges, so the audit row is committed atomically with the
/// correction it describes. It must therefore only Add(); calling SaveChanges here would break
/// that guarantee.
/// </summary>
public class AppraisalPropertyCorrectionAuditLogWriter(
    AppraisalDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AppraisalPropertyCorrectionAuditLogWriter> logger)
    : INotificationHandler<AppraisalPropertyCorrectedEvent>
{
    public Task Handle(AppraisalPropertyCorrectedEvent notification, CancellationToken cancellationToken)
    {
        var log = new AppraisalPropertyCorrectionLog(
            appraisalId: notification.AppraisalId,
            appraisalPropertyId: notification.PropertyId,
            propertyType: notification.PropertyType,
            changedFields: notification.ChangedFields,
            reason: notification.Reason,
            changedBy: notification.By,
            changedAt: dateTimeProvider.ApplicationNow);

        dbContext.AppraisalPropertyCorrectionLogs.Add(log);

        logger.LogInformation(
            "[DATA-CORRECTION] Appraisal {AppraisalId} property {PropertyId} corrected by {By}: {ChangedFields}",
            notification.AppraisalId, notification.PropertyId, notification.By, notification.ChangedFields);

        return Task.CompletedTask;
    }
}
