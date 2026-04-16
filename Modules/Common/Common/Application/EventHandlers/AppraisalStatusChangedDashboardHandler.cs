using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

/// <summary>
/// Adjusts <c>common.AppraisalStatusSummaries</c> counts whenever an appraisal
/// transitions between lifecycle statuses.
///
/// Wired to <see cref="AppraisalStatusChangedIntegrationEvent"/> which carries
/// <c>PreviousStatus</c> and <c>NewStatus</c> string values.
///
/// TODO: user to provide derivation rules for
/// Pending → InProgress → UnderReview → Completed → Cancelled transitions
/// (e.g., should Cancelled always decrement its previous status bucket?).
/// </summary>
public class AppraisalStatusChangedDashboardHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalStatusChangedDashboardHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<AppraisalStatusChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalStatusChangedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: AppraisalStatusChanged for RequestId {RequestId}: {Previous} → {New}",
            message.RequestId,
            message.PreviousStatus,
            message.NewStatus);

        var connection = connectionFactory.GetOpenConnection();
        var now = DateTimeOffset.UtcNow;

        // TODO: derivation rules pending — placeholder decrements previous bucket and
        // increments new bucket. Confirm edge cases (e.g., Cancelled from any status) with user.
        using var transaction = connection.BeginTransaction();
        try
        {
            // Decrement previous status bucket
            await connection.ExecuteAsync("""
                MERGE common.AppraisalStatusSummaries WITH (HOLDLOCK) AS target
                USING (SELECT @Status AS Status) AS source
                ON target.Status = source.Status
                WHEN MATCHED THEN
                    UPDATE SET Count = CASE WHEN Count > 0 THEN Count - 1 ELSE 0 END, LastUpdatedAt = @Now
                WHEN NOT MATCHED THEN
                    INSERT (Status, Count, LastUpdatedAt)
                    VALUES (@Status, 0, @Now);
                """,
                new { Status = message.PreviousStatus, Now = now }, transaction: transaction);

            // Increment new status bucket
            await connection.ExecuteAsync("""
                MERGE common.AppraisalStatusSummaries WITH (HOLDLOCK) AS target
                USING (SELECT @Status AS Status) AS source
                ON target.Status = source.Status
                WHEN MATCHED THEN
                    UPDATE SET Count = Count + 1, LastUpdatedAt = @Now
                WHEN NOT MATCHED THEN
                    INSERT (Status, Count, LastUpdatedAt)
                    VALUES (@Status, 1, @Now);
                """,
                new { Status = message.NewStatus, Now = now }, transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
