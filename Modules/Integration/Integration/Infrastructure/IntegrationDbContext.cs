using Integration.Domain.IdempotencyRecords;
using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure.FileInterface;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Extensions;
using System.Reflection;

namespace Integration.Infrastructure;

public class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : DbContext(options)
{
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<FileInterfaceConfigEntity> FileInterfaceConfigs => Set<FileInterfaceConfigEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("integration");
        modelBuilder.ApplyGlobalConventions();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
