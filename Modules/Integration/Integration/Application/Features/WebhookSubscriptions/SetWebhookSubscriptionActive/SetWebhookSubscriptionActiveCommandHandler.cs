using Integration.Infrastructure.Repositories;
using MediatR;
using Shared.CQRS;
using Shared.Exceptions;

namespace Integration.Application.Features.WebhookSubscriptions.SetWebhookSubscriptionActive;

public record SetWebhookSubscriptionActiveCommand(Guid Id, bool IsActive) : ICommand;

public class SetWebhookSubscriptionActiveCommandHandler(IWebhookSubscriptionRepository repository)
    : ICommandHandler<SetWebhookSubscriptionActiveCommand>
{
    public async Task<Unit> Handle(SetWebhookSubscriptionActiveCommand command, CancellationToken cancellationToken)
    {
        var subscription = await repository.GetByIdAsync(command.Id, cancellationToken)
                           ?? throw new NotFoundException("WebhookSubscription", command.Id);

        if (command.IsActive)
            subscription.Activate();
        else
            subscription.Deactivate();

        await repository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
