namespace Shared.Messaging.OutboxPatterns.Services;

public interface IOutboxService
{
    Task PublishEvent(CancellationToken cancellationToken = default);
}