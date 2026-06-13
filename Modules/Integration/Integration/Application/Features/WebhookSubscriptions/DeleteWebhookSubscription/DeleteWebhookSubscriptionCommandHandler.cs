using Dapper;
using Integration.Infrastructure.Repositories;
using MediatR;
using Shared.CQRS;
using Shared.Data;
using Shared.Exceptions;

namespace Integration.Application.Features.WebhookSubscriptions.DeleteWebhookSubscription;

public record DeleteWebhookSubscriptionCommand(Guid Id) : ICommand;

public class DeleteWebhookSubscriptionCommandHandler(
    IWebhookSubscriptionRepository repository,
    ISqlConnectionFactory sqlConnectionFactory)
    : ICommandHandler<DeleteWebhookSubscriptionCommand>
{
    public async Task<Unit> Handle(DeleteWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var subscription = await repository.GetByIdAsync(command.Id, cancellationToken)
                           ?? throw new NotFoundException("WebhookSubscription", command.Id);

        // Deliveries reference the subscription and carry the audit trail — preserve them. If any
        // exist, block hard-delete and steer the admin to deactivate instead.
        var connection = sqlConnectionFactory.GetOpenConnection();
        var deliveryCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM integration.WebhookDeliveries WHERE SubscriptionId = @Id",
            new { command.Id });

        if (deliveryCount > 0)
            throw new ConflictException(
                "This subscription has delivery history and cannot be deleted. Deactivate it instead.");

        await repository.DeleteAsync(subscription, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
