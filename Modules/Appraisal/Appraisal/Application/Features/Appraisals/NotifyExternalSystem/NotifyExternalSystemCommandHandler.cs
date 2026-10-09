using System.Text.Json;
using Appraisal.Infrastructure;
using Dapper;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.NotifyExternalSystem;

/// <summary>
/// Publishes AppraisalResultReadyIntegrationEvent (the event the source-system webhook listens to) for a
/// Completed appraisal and records one audit row. Event and row are saved together by TransactionalBehavior,
/// so the history cannot claim a notification that was never queued, or the reverse.
/// </summary>
public class NotifyExternalSystemCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    IIntegrationEventOutbox outbox,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<NotifyExternalSystemCommand, NotifyExternalSystemResult>
{
    public async Task<NotifyExternalSystemResult> Handle(
        NotifyExternalSystemCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        if (appraisal.Status != AppraisalStatus.Completed || appraisal.CompletedAt is null)
        {
            throw new ConflictException(
                $"Appraisal is {appraisal.Status.Code}. The source system can only be notified for " +
                "Completed appraisals.",
                "APPRAISAL_NOT_COMPLETED");
        }

        var connection = connectionFactory.GetOpenConnection();
        var externalSystem =
            (await AppraisalExternalSourceQuery.GetAsync(connection, command.AppraisalId, cancellationToken))
            ?.ExternalSystem
            ?? throw new ConflictException(
                "This appraisal has no source system to notify (no external case key or system, or no active webhook subscription).",
                "NO_EXTERNAL_SOURCE");

        // Same meaning the auto-attach job gives the flag: is a post-approval summary in the package the source
        // will collect. A pre-approval D042/D043 does not count (see HasPostCompletionSummaryAsync in the job).
        // D042 = Construction Progress Inspection Summary, D043 = Property Valuation Summary.
        var documentReady = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM [appraisal].[AppraisalDocuments]
                    WHERE [AppraisalId] = @AppraisalId AND [DocumentTypeCode] IN ('D042', 'D043')
                      AND [CreatedAt] >= @CompletedAt
                ) THEN 1 ELSE 0 END
                """,
                new { command.AppraisalId, CompletedAt = appraisal.CompletedAt.Value },
                cancellationToken: cancellationToken));

        outbox.Publish(
            new AppraisalResultReadyIntegrationEvent
            {
                AppraisalId = appraisal.Id,
                RequestId = appraisal.RequestId,
                CompletedAt = appraisal.CompletedAt.Value,
                DocumentReady = documentReady,
                FailureReason = documentReady ? null : "No appraisal summary attached"
            },
            correlationId: appraisal.Id.ToString());

        dbContext.AppraisalPropertyCorrectionLogs.Add(AppraisalPropertyCorrectionLog.ForDocuments(
            command.AppraisalId,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ExternalNotification"] = new { from = (string?)null, to = externalSystem },
            }),
            command.Reason.Trim(),
            currentUser.UserCode ?? currentUser.Username ?? "unknown",
            dateTimeProvider.ApplicationNow));

        return new NotifyExternalSystemResult(externalSystem);
    }
}
