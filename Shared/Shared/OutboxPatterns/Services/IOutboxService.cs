using Microsoft.EntityFrameworkCore;

namespace Shared.OutboxPatterns.Services;

public interface IOutboxService
{
    Task<short> PublishEvent(CancellationToken cancellationToken = default);
}