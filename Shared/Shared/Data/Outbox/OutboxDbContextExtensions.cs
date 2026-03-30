using Microsoft.EntityFrameworkCore;

namespace Shared.Data.Outbox;

public static class OutboxDbContextExtensions
{
    /// <summary>
    /// Adds outbox + delivery lock + inbox tables. For modules that PUBLISH integration events.
    /// </summary>
    public static ModelBuilder AddIntegrationEventOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IntegrationEventOutboxConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxDeliveryLockConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        return modelBuilder;
    }

    /// <summary>
    /// Adds inbox table only. For modules that only CONSUME integration events.
    /// </summary>
    public static ModelBuilder AddIntegrationEventInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        return modelBuilder;
    }
}
