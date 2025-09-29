using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Collateral.Collateral.Shared.EventHandlers;

public class RequestSubmittedIntegrationEventHandler(ILogger<RequestSubmittedIntegrationEvent> logger, ICollateralService collateralService)
    : IConsumer<RequestSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        logger.LogInformation("Integration Event handled: {IntegrationEvent}", context.Message.GetType().Name);

        await collateralService.CreateDefaultCollateral(context.Message.RequestTitles);
    }
}