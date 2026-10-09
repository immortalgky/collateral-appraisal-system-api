using System.Data;
using Dapper;

namespace Appraisal.Application.Features.Appraisals.NotifyExternalSystem;

/// <summary>
/// Which external system (if any) an appraisal came from, plus its CompletedAt. One definition for the
/// data-correction page's "notify source system" affordances, so they cannot drift from when the webhook really sends.
/// </summary>
public static class AppraisalExternalSourceQuery
{
    /// <summary>
    /// Null when the appraisal does not exist; otherwise the record, whose <see cref="ExternalSource.ExternalSystem"/>
    /// is null unless the request has an ExternalCaseKey AND an ExternalSystem AND the appraisal has a number —
    /// the keys AppraisalCompletedWebhookConsumer (Integration module) requires — AND that system has an active
    /// APPRAISAL_COMPLETED (or catch-all) webhook subscription, the row WebhookService.SendAsync looks up.
    /// Without the last check the history would record a notification that WebhookService silently drops.
    /// </summary>
    public static async Task<ExternalSource?> GetAsync(
        IDbConnection connection, Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(
                """
                SELECT a.[AppraisalNumber], r.[ExternalCaseKey], r.[ExternalSystem], a.[CompletedAt],
                       -- Same match as WebhookSubscriptionRepository.GetBySubscriptionAsync (envelope sends).
                       CAST(CASE WHEN EXISTS (
                           SELECT 1 FROM [integration].[WebhookSubscriptions] ws
                           WHERE ws.[SystemCode] = r.[ExternalSystem]
                             AND (ws.[EventType] = 'APPRAISAL_COMPLETED' OR ws.[EventType] IS NULL)
                             AND ws.[IsActive] = 1
                       ) THEN 1 ELSE 0 END AS bit) AS [HasSubscription]
                FROM [appraisal].[Appraisals] a
                LEFT JOIN [request].[Requests] r ON r.[Id] = a.[RequestId]
                WHERE a.[Id] = @AppraisalId AND a.[IsDeleted] = 0
                """,
                new { AppraisalId = appraisalId },
                cancellationToken: cancellationToken));

        if (row is null) return null;

        var sends = !string.IsNullOrEmpty(row.AppraisalNumber)
                    && !string.IsNullOrEmpty(row.ExternalCaseKey)
                    && !string.IsNullOrEmpty(row.ExternalSystem)
                    && row.HasSubscription;
        return new ExternalSource(sends ? row.ExternalSystem : null, row.CompletedAt);
    }

    // Dapper binds positionally — keep this in the SELECT's column order.
    private sealed record Row(
        string? AppraisalNumber, string? ExternalCaseKey, string? ExternalSystem, DateTime? CompletedAt,
        bool HasSubscription);
}

/// <param name="CompletedAt">Approval time (Appraisals.CompletedAt), shown in the data-correction page header.</param>
public sealed record ExternalSource(string? ExternalSystem, DateTime? CompletedAt);
