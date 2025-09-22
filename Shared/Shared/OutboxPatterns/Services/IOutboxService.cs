using Microsoft.EntityFrameworkCore;

namespace Shared.OutboxPatterns.Services;

public interface IOutboxService
{
    Task<int> PublishEvent(CancellationToken cancellationToken = default);
}