using Dapper;
using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Appraisal.Application.Features.Quotations.CloseQuotation;

namespace Appraisal.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that automatically closes quotation requests whose DueDate has passed.
/// Polls every 60 seconds. For each overdue "Sent" quotation:
///   1. Sends CloseQuotationCommand (idempotent) — transitions status to UnderAdminReview.
///   2. Publishes QuotationDueDatePassedIntegrationEvent so downstream modules can expire
///      fan-out PendingTasks and auto-decline unresponsive CompanyQuotations.
/// </summary>
public sealed class QuotationAutoCloseService(
    IServiceScopeFactory scopeFactory,
    ILogger<QuotationAutoCloseService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("QuotationAutoCloseService started. Scanning every {ScanIntervalSeconds}s",
            ScanInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndCloseOverdueQuotationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during quotation auto-close scan");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        logger.LogInformation("QuotationAutoCloseService stopped");
    }

    private async Task ScanAndCloseOverdueQuotationsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
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
                  AND DueDate <= @now
                """,
                new { now = DateTime.UtcNow });
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

                // Signal downstream modules to expire fan-out tasks and auto-decline companies.
                // Idempotent: Workflow consumer guards against already-terminal tasks.
                await publishEndpoint.Publish(new QuotationDueDatePassedIntegrationEvent
                {
                    QuotationRequestId = row.Id,
                    QuotationWorkflowInstanceId = row.QuotationWorkflowInstanceId
                }, ct);

                logger.LogInformation(
                    "QuotationAutoCloseService: published QuotationDueDatePassedIntegrationEvent for {QuotationRequestId}",
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
