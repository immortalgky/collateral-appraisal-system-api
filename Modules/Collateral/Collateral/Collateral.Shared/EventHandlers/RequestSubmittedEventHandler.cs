using Collateral.Data;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Collateral.Collateral.Shared.EventHandlers;

public class RequestSubmittedIntegrationEventHandler(
    ILogger<RequestSubmittedIntegrationEvent> logger,
    ICollateralService collateralService,
    InboxGuard<CollateralDbContext> inboxGuard)
    : IConsumer<RequestSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        logger.LogInformation("Integration Event handled: {IntegrationEvent}", context.Message.GetType().Name);

        await collateralService.CreateDefaultCollateral(context.Message.RequestTitles);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}