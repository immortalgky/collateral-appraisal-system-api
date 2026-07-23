using Microsoft.EntityFrameworkCore;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using Workflow.Data;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using static Workflow.Workflow.Services.WorkflowSignals;

namespace Workflow.EventHandlers;

/// <summary>
/// Links the newly created Appraisal to its workflow instance and dispatches the
/// "AppraisalCreated" signal that resumes the await-appraisal-created AwaitSignalActivity.
///
/// Idempotency: the InboxMessage INSERT is staged into the SAME SaveChangesAsync/transaction as
/// the work (M4 fix, mirroring AppointmentDateChangedIntegrationEventConsumer). Using
/// InboxGuard.TryClaimAsync here was unsafe: it commits the "Processing" claim in its own
/// transaction, so when the work rolled back every MassTransit retry landed inside the guard's
/// 5-minute stale window, returned "skip", and the message was acked and silently lost — leaving
/// the workflow parked at await-appraisal-created forever.
///
/// Concurrency: WorkflowInstance carries a RowVersion token. The instance is loaded INSIDE the
/// transaction (and inside the execution-strategy delegate) so a retry always re-reads a fresh
/// token, and a lost optimistic-concurrency race is retried a bounded number of times rather than
/// failing the message.
///
/// Ordering: hosted on the shared "workflow-instance-variables" endpoint, partitioned by RequestId
/// alongside the other WorkflowInstance variable feeders, so this resume can no longer run
/// concurrently with AppraisalValueChanged/AppointmentDateChanged for the same workflow instance.
/// [ExcludeFromConfigureEndpoints] keeps ConfigureEndpoints from also creating an unordered
/// auto-queue that would reintroduce the race.
/// </summary>
[ExcludeFromConfigureEndpoints]
public class AppraisalCreatedIntegrationEventConsumer(
    ILogger<AppraisalCreatedIntegrationEventConsumer> logger,
    WorkflowDbContext dbContext,
    IWorkflowInstanceRepository workflowInstanceRepository,
    IWorkflowSignalDispatcher signalDispatcher,
    IWorkflowUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    InboxGuard<WorkflowDbContext> inboxGuard) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    // Matches InboxGuard.StaleThresholdMinutes so the two share the same reclaim window.
    private const int StaleThresholdMinutes = 5;

    private const int MaxConcurrencyAttempts = 3;

    public async Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId;
        var consumerName = GetType().Name;
        var message = context.Message;

        // Idempotency check — read-only, no commit yet. The claim itself is staged later so it
        // shares the fate of the work.
        if (await AlreadyHandledAsync(messageId, consumerName, ct))
            return;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            nameof(AppraisalCreatedIntegrationEvent),
            message.AppraisalId,
            message.RequestId);

        var correlationValue = message.RequestId.ToString();

        var appraisalPayload = new Dictionary<string, object>
        {
            ["appraisalId"] = message.AppraisalId,
            ["appraisalNumber"] = message.AppraisalNumber ?? string.Empty,
            ["appraisalType"] = message.AppraisalType ?? "New"
        };

        // When the creation request included an appointment date, thread it into the
        // same payload so Variables["appointmentDate"] is written atomically with the
        // appraisal-created signal — no second consumer, no RowVersion race.
        if (message.AppointmentDateTime.HasValue)
            appraisalPayload["appointmentDate"] = message.AppointmentDateTime.Value;

        try
        {
            for (var attempt = 1;; attempt++)
            {
                try
                {
                    await LinkAndDispatchAsync(messageId, consumerName, correlationValue, appraisalPayload,
                        message.RequestId, message.AppraisalId, ct);
                    break;
                }
                catch (Exception ex) when (attempt < MaxConcurrencyAttempts && IsConcurrencyConflict(ex))
                {
                    logger.LogWarning(ex,
                        "Concurrency conflict linking AppraisalId {AppraisalId} for RequestId {RequestId}; " +
                        "attempt {Attempt}/{MaxAttempts}, reloading",
                        message.AppraisalId, message.RequestId, attempt, MaxConcurrencyAttempts);

                    await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), ct);
                }
            }

            await inboxGuard.MarkAsProcessedAsync(messageId, consumerName, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error linking AppraisalId {AppraisalId} to workflow for RequestId: {RequestId}",
                message.AppraisalId, message.RequestId);
            throw;
        }
    }

    /// <summary>
    /// True when the failure is a lost optimistic-concurrency race. Walks the inner-exception chain:
    /// WorkflowEngine/WorkflowService rethrow DbUpdateConcurrencyException untouched today, but the
    /// resume path is deep enough that an intermediate layer wrapping it must not silently disable
    /// the retry.
    /// </summary>
    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException)
                return true;

        return false;
    }

    /// <summary>
    /// One retryable unit: load the instance, write the appraisal variables, dispatch the signal
    /// (which resumes the workflow) and stage the inbox claim — all inside a single transaction.
    /// </summary>
    private async Task LinkAndDispatchAsync(
        Guid? messageId,
        string consumerName,
        string correlationValue,
        Dictionary<string, object> appraisalPayload,
        Guid requestId,
        Guid appraisalId,
        CancellationToken ct)
    {
        // Wrap in execution strategy + transaction so that
        // SqlServerRetryingExecutionStrategy allows DB operations inside
        // the user-initiated transaction, and ResumeWorkflowAsync takes
        // the HasActiveTransaction branch to defer commit.
        var strategy = unitOfWork.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A previous attempt may have left a half-applied graph (and a stale RowVersion) in
            // the tracker; the strategy can also re-enter this delegate after a transient fault.
            dbContext.ChangeTracker.Clear();

            await unitOfWork.BeginTransactionAsync(ct);
            try
            {
                // Loaded INSIDE the transaction so the RowVersion we write against is fresh.
                var instance = await workflowInstanceRepository.GetByCorrelationId(correlationValue, ct);

                if (instance is null)
                {
                    logger.LogWarning(
                        "No workflow found for RequestId: {RequestId}. AppraisalId {AppraisalId} will not be linked.",
                        requestId, appraisalId);
                }
                else
                {
                    instance.UpdateVariables(appraisalPayload);

                    await signalDispatcher.DispatchAsync(
                        signalName: AppraisalCreated,
                        correlationValue: correlationValue,
                        payload: appraisalPayload,
                        ct);
                }

                // Stage the claim so it commits with the work — or rolls back with it, leaving no
                // "Processing" row to block MassTransit's redelivery.
                StageInboxMessage(messageId, consumerName);

                await unitOfWork.SaveChangesAsync(ct);
                await unitOfWork.CommitTransactionAsync(ct);

                if (instance is not null)
                    logger.LogInformation(
                        "Linked AppraisalId {AppraisalId} to workflow {WorkflowInstanceId} and dispatched signal for RequestId: {RequestId}",
                        appraisalId, instance.Id, requestId);
            }
            catch
            {
                try { await unitOfWork.RollbackTransactionAsync(ct); }
                catch (Exception rollbackEx)
                {
                    logger.LogError(rollbackEx, "Failed to rollback transaction for RequestId: {RequestId}",
                        requestId);
                }

                throw;
            }
        });
    }

    /// <summary>
    /// Read-only inbox pre-check. Returns true when the message must be skipped. A stale
    /// "Processing" row is deleted so the staged INSERT later in this consume does not collide.
    /// </summary>
    private async Task<bool> AlreadyHandledAsync(Guid? messageId, string consumerName, CancellationToken ct)
    {
        if (!messageId.HasValue) return false;

        var existing = await dbContext.Set<InboxMessage>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MessageId == messageId.Value
                                      && m.ConsumerType == consumerName, ct);

        if (existing is null) return false;

        if (existing.Status == InboxMessageStatus.Processed)
            return true;

        // Processing but still within the live window — another consumer is handling it.
        if (existing.Status == InboxMessageStatus.Processing
            && existing.StartedAt >= dateTimeProvider.ApplicationNow.AddMinutes(-StaleThresholdMinutes))
            return true;

        // Stale Processing row: remove it so our coming INSERT doesn't hit a PK collision.
        if (existing.Status == InboxMessageStatus.Processing)
        {
            var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";
            await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM [" + schema + "].[InboxMessage] " +
                "WHERE MessageId = {0} AND ConsumerType = {1} AND Status = 'Processing'",
                new object[] { messageId.Value, consumerName }, ct);
        }

        return false;
    }

    private void StageInboxMessage(Guid? messageId, string consumerName)
    {
        if (!messageId.HasValue) return;

        dbContext.Set<InboxMessage>().Add(
            InboxMessage.Create(messageId.Value, consumerName, dateTimeProvider.ApplicationNow));
    }
}
