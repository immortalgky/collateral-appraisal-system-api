namespace Shared.Messaging.OutboxPatterns.Services;

public interface IOutboxService
{
    Task<int> PublishEvent(CancellationToken cancellationToken = default);
}