using Appraisal.Domain.Appraisals;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Round-trips the outcome of the async LOS PMA delivery (published by the Integration module's
/// WebhookDispatchConsumer after attempting all of a property's title deliveries) onto the
/// AppraisalProperty that was pushed, so the UI can show Synced/Pending/Failed and a future
/// submit-gate can block progression while a property hasn't delivered.
/// Pattern: AppraisalApprovedIntegrationEventHandler.
///
/// No InboxGuard — the status write is idempotent (SetExternalSyncStatus just assigns
/// status/error/timestamp, no accumulation), so at-least-once redelivery is safe and avoids the
/// claim-before-process stranding hazard (a transient SaveChanges failure after a committed claim
/// would otherwise leave the property stuck at Pending until the stale-reclaim window passes).
///
/// <see cref="ExcludeFromConfigureEndpointsAttribute"/> — bound to a dedicated partitioned
/// "pma-sync-status" endpoint (Program.cs), partitioned by AppraisalId, so the round-tripped status
/// events serialize relative to each other (not relative to the synchronous Pending write done by
/// the PMA save command handler, which happens outside this partition — a stale Delivered/Failed
/// can briefly overwrite a newer save's Pending until that save's own status event arrives; this is
/// self-correcting).
/// </summary>
[ExcludeFromConfigureEndpoints]
public class PmaExternalSyncStatusChangedIntegrationEventHandler(
    ILogger<PmaExternalSyncStatusChangedIntegrationEventHandler> logger,
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
    : IConsumer<PmaExternalSyncStatusChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PmaExternalSyncStatusChangedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        try
        {
            var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(message.AppraisalId, ct);

            if (appraisal is null)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found when handling {IntegrationEvent} — no-op",
                    message.AppraisalId,
                    nameof(PmaExternalSyncStatusChangedIntegrationEvent));
                return;
            }

            var property = appraisal.GetProperty(message.PropertyId);

            if (property is null)
            {
                logger.LogWarning(
                    "Property {PropertyId} not found on Appraisal {AppraisalId} when handling {IntegrationEvent} — no-op",
                    message.PropertyId,
                    message.AppraisalId,
                    nameof(PmaExternalSyncStatusChangedIntegrationEvent));
                return;
            }

            property.SetExternalSyncStatus(message.Status, message.Error, dateTimeProvider.ApplicationNow);

            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation(
                "External sync status updated for AppraisalId {AppraisalId} PropertyId {PropertyId}: {Status}",
                message.AppraisalId,
                message.PropertyId,
                message.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing {IntegrationEvent} for AppraisalId: {AppraisalId} PropertyId: {PropertyId}",
                nameof(PmaExternalSyncStatusChangedIntegrationEvent),
                message.AppraisalId,
                message.PropertyId);

            throw;
        }
    }
}
