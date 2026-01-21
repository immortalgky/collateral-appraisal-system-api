using Integration.Domain.WebhookDeliveries;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Integration.Infrastructure.Repositories;

public interface IWebhookDeliveryRepository : IRepository<WebhookDelivery, Guid>
{
    Task<List<WebhookDelivery>> GetPendingRetriesAsync(DateTime now, int maxCount = 100, CancellationToken cancellationToken = default);
}

public class WebhookDeliveryRepository(IntegrationDbContext dbContext)
    : BaseRepository<WebhookDelivery, Guid>(dbContext), IWebhookDeliveryRepository
{
    private readonly IntegrationDbContext _dbContext = dbContext;

    public async Task<List<WebhookDelivery>> GetPendingRetriesAsync(
        DateTime now,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookDeliveries
            .Where(x => x.Status == DeliveryStatus.Retrying && x.NextRetryAt <= now)
            .OrderBy(x => x.NextRetryAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }
}
