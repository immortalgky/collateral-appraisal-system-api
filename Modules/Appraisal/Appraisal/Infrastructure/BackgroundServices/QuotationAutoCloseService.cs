using Dapper;
using MassTransit;
using Shared.Messaging.Events;
using Shared.Messaging.Services;
using Shared.Time;
using Appraisal.Application.Features.Quotations.CloseQuotation;

namespace Appraisal.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that automatically closes quotation requests whose CutOffTime has passed.
/// Polls every 60 seconds. For each overdue "Sent" quotation:
///   1. Sends CloseQuotationCommand (idempotent) — transitions status to UnderAdminReview.
///   2. Publishes QuotationCutOffTimePassedIntegrationEvent so downstream modules can expire
///      fan-out PendingTasks and auto-decline unresponsive CompanyQuotations.
///
/// Distributed-safe: only the lease holder runs the scan; standby instances wait.
/// </summary>
public sealed class QuotationAutoCloseService(
    IServiceScopeFactory scopeFactory,
    ILogger<QuotationAutoCloseService> logger,
    IDateTimeProvider dateTimeProvider)
    : LeasedBackgroundService<AppraisalDbContext>(scopeFactory, logger, dateTimeProvider)
{
    protected override string LockId => "AppraisalDbContext-QuotationAutoClose";

    protected override async Task ExecuteWhileLeasedAsync(IServiceScope scope, CancellationToken ct)
    {
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        IEnumerable<OverdueQuotationRow> overdueRows;
        using (var connection = connectionFactory.GetOpenConnection())
        {
            overdueRows = await connection.QueryAsync<OverdueQuotationRow>(
                """
                SELECT Id, QuotationWorkflowInstanceId
                FROM appraisal.QuotationRequests
                WHERE Status = 'Sent'
                  AND CutOffTime <= @now
                """,
                new { now = dateTimeProvider.ApplicationNow });
        }

        var rows = overdueRows.ToList();
        if (rows.Count == 0)
        {
            logger.LogDebug("QuotationAutoCloseService: no overdue quotations found");
            return;
        }

        logger.LogInformation("QuotationAutoCloseService: closing {Count} overdue quotation(s)", rows.Count);

        foreach (var row in rows)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await mediator.Send(new CloseQuotationCommand(row.Id), ct);
                logger.LogInformation("Auto-closed QuotationRequest {QuotationRequestId}", row.Id);

                await publishEndpoint.Publish(new QuotationCutOffTimePassedIntegrationEvent
                {
                    QuotationRequestId = row.Id,
                    QuotationWorkflowInstanceId = row.QuotationWorkflowInstanceId
                }, ct);

                logger.LogInformation(
                    "QuotationAutoCloseService: published QuotationCutOffTimePassedIntegrationEvent for {QuotationRequestId}",
                    row.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process overdue QuotationRequest {QuotationRequestId}", row.Id);
            }
        }
    }

    private sealed record OverdueQuotationRow(Guid Id, Guid? QuotationWorkflowInstanceId);
}