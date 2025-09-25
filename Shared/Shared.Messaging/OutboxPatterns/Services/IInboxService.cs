namespace Shared.Messaging.OutboxPatterns.Services;

public interface IInboxService
{
    Task CheckDuplicate(Guid id, CancellationToken cancellationToken = default);
    Task AddMessageInboxAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class;
    Task ClearTimeOutMessage(CancellationToken cancellationToken = default);
}