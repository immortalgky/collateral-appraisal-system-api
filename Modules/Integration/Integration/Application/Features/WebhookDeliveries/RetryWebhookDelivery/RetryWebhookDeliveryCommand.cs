using MediatR;
using Shared.CQRS;

namespace Integration.Application.Features.WebhookDeliveries.RetryWebhookDelivery;

public record RetryWebhookDeliveryCommand(Guid Id) : ICommand<Unit>;
