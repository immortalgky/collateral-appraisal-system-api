using Integration.Application.Services;
using MediatR;
using Shared.CQRS;

namespace Integration.Application.Features.WebhookDeliveries.RetryWebhookDelivery;

public class RetryWebhookDeliveryCommandHandler(IWebhookService webhookService)
    : ICommandHandler<RetryWebhookDeliveryCommand>
{
    public async Task<Unit> Handle(
        RetryWebhookDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        await webhookService.ResendAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}
