using MassTransit.EntityFrameworkCoreIntegration;

namespace Shared.Messaging.OutboxPatterns.Services;

public interface IInboxService
{
    Task<bool> CheckDuplicate(Guid id, CancellationToken cancellationToken = default);
    Task<bool> AddMessageInboxAsync<TMessage>(ConsumeContext<TMessage> message, CancellationToken cancellationToken = default) where TMessage : class;
    Task ClearTimeOutMessage(CancellationToken cancellationToken = default);
}