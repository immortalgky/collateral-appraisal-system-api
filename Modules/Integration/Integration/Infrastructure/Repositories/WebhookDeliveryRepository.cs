using Integration.Domain.WebhookDeliveries;
using Shared.Data;

namespace Integration.Infrastructure.Repositories;

public interface IWebhookDeliveryRepository : IRepository<WebhookDelivery, Guid>
{
}

public class WebhookDeliveryRepository(IntegrationDbContext dbContext)
    : BaseRepository<WebhookDelivery, Guid>(dbContext), IWebhookDeliveryRepository
{
}
