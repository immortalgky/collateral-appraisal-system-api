using Integration.Domain.WebhookSubscriptions;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Integration.Infrastructure.Repositories;

public interface IWebhookSubscriptionRepository : IRepository<WebhookSubscription, Guid>
{
    Task<WebhookSubscription?> GetBySystemCodeAsync(string systemCode, CancellationToken cancellationToken = default);
    Task<List<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default);
}

public class WebhookSubscriptionRepository(IntegrationDbContext dbContext)
    : BaseRepository<WebhookSubscription, Guid>(dbContext), IWebhookSubscriptionRepository
{
    private readonly IntegrationDbContext _dbContext = dbContext;

    public async Task<WebhookSubscription?> GetBySystemCodeAsync(
        string systemCode,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(x => x.SystemCode == systemCode && x.IsActive, cancellationToken);
    }

    public async Task<List<WebhookSubscription>> GetActiveSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookSubscriptions
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
    }
}
