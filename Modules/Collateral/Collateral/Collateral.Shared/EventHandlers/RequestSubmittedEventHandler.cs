using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Collateral.Collateral.Shared.EventHandlers;

public class RequestSubmittedIntegrationEventHandler(ILogger<RequestSubmittedIntegrationEvent> logger, ICollateralRepository collateralRepository)
    : IConsumer<RequestSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        logger.LogInformation("Integration Event handled: {IntegrationEvent}", context.Message.GetType().Name);

        foreach (var requestTitleDto in context.Message.RequestTitles)
        {
            var collateralMaster = CollateralMaster.Create("land", null);
            await collateralRepository.CreateCollateralMasterAsync(collateralMaster);
            var collateralLand = CollateralLand.FromRequestTitleDto(collateralMaster.Id, requestTitleDto);
            collateralMaster.SetCollateralLand(collateralLand);
        }

        await collateralRepository.SaveChangesAsync();
    }
}