using Appraisal.Application.Features.Quotations.AutoDeclineCompanyQuotation;
using MassTransit;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Auto-declines CompanyQuotations for companies that did not respond by the quotation DueDate.
/// Triggered after the Workflow module expires the corresponding fan-out PendingTasks.
///
/// Idempotent: AutoDeclineCompanyQuotationCommandHandler skips already-terminal quotations.
/// </summary>
public class QuotationCompaniesAutoExpiredIntegrationEventHandler(
    ISender mediator,
    InboxGuard<AppraisalDbContext> inboxGuard,
    ILogger<QuotationCompaniesAutoExpiredIntegrationEventHandler> logger)
    : IConsumer<QuotationCompaniesAutoExpiredIntegrationEvent>
{
    private const string AutoExpireReason = "Auto-expired — no response by due date";

    public Task Consume(ConsumeContext<QuotationCompaniesAutoExpiredIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<QuotationCompaniesAutoExpiredIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "QuotationCompaniesAutoExpiredHandler: QuotationRequestId={QuotationRequestId}, ExpiredCompanyCount={Count}",
            message.QuotationRequestId, message.ExpiredCompanyIds.Count);

        foreach (var companyId in message.ExpiredCompanyIds)
        {
            try
            {
                await mediator.Send(
                    new AutoDeclineCompanyQuotationCommand(message.QuotationRequestId, companyId, AutoExpireReason),
                    ct);

                logger.LogInformation(
                    "QuotationCompaniesAutoExpiredHandler: auto-declined company {CompanyId} on QuotationRequest {QuotationRequestId}",
                    companyId, message.QuotationRequestId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "QuotationCompaniesAutoExpiredHandler: failed to auto-decline company {CompanyId} on QuotationRequest {QuotationRequestId}",
                    companyId, message.QuotationRequestId);
            }
        }
    }
}
