using Integration.Domain.WebhookSubscriptions;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Integration.Infrastructure.Repositories;

public interface IWebhookSubscriptionRepository : IRepository<WebhookSubscription, Guid>
{
    /// <summary>
    /// Resolves the active subscription for a (SystemCode, EventType) pair. When
    /// <paramref name="exactMatchOnly"/> is false (default): an exact match wins; otherwise falls
    /// back to the SystemCode's catch-all row (EventType IS NULL), if any — lets one downstream
    /// system keep a single default subscription for most events while a specific event (e.g. a
    /// different auth model/callback) gets its own dedicated row. When true: returns ONLY the exact
    /// match, never the catch-all — required for bare-payload sends (see WebhookService.SendAsync)
    /// where falling back to the envelope catch-all would silently sign/POST with the wrong
    /// auth/method/shape.
    /// </summary>
    Task<WebhookSubscription?> GetBySubscriptionAsync(
        string systemCode, string eventType, bool exactMatchOnly = false, CancellationToken cancellationToken = default);

    Task<List<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default);
}

public class WebhookSubscriptionRepository(IntegrationDbContext dbContext)
    : BaseRepository<WebhookSubscription, Guid>(dbContext), IWebhookSubscriptionRepository
{
    private readonly IntegrationDbContext _dbContext = dbContext;

    public async Task<WebhookSubscription?> GetBySubscriptionAsync(
        string systemCode,
        string eventType,
        bool exactMatchOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (exactMatchOnly)
        {
            return await _dbContext.WebhookSubscriptions
                .FirstOrDefaultAsync(
                    x => x.SystemCode == systemCode && x.EventType == eventType && x.IsActive,
                    cancellationToken);
        }

        // Single round-trip: candidates are the exact (SystemCode, EventType) match plus the
        // SystemCode's catch-all (EventType IS NULL) row. Sort the exact match first so it wins
        // over the catch-all when both exist — deterministic given the unfiltered unique index on
        // (SystemCode, EventType) guarantees at most one row of each kind.
        return await _dbContext.WebhookSubscriptions
            .Where(x => x.SystemCode == systemCode && (x.EventType == eventType || x.EventType == null) && x.IsActive)
            .OrderBy(x => x.EventType == null ? 1 : 0)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<WebhookSubscription>> GetActiveSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookSubscriptions
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
    }
}
