namespace Shared.Messaging.OutboxPatterns.Extensions;

public static class Outbox
{
    /// <summary>
    /// Outbox-Patten ("Outbox" is Messages Output)
    /// Outbox services for DbContext
    /// IOutboxService BackgroundService
    /// </summary>
    public static IServiceCollection AddOutbox<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : DbContext
    {       
        // Register repositories with keyed services for specific DbContext type
        var dbContextName = typeof(TDbContext).Name;
        
        services.AddKeyedScoped<IOutboxRepository>(dbContextName, (provider, key) => 
            new OutboxRepository<TDbContext>(provider.GetRequiredService<TDbContext>()));
        
        services.AddKeyedScoped<IOutboxReadRepository>(dbContextName, (provider, key) => 
            new OutboxReadRepository<TDbContext>(
                provider.GetRequiredService<TDbContext>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ISqlConnectionFactory>(),
                dbContextName));

        // Register service with the specific schema for this DbContext
        services.AddKeyedScoped<IOutboxService>(dbContextName, (provider, key) => 
            new OutboxService(
                provider.GetRequiredService<IPublishEndpoint>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredKeyedService<IOutboxReadRepository>(key),
                provider.GetRequiredKeyedService<IOutboxRepository>(key),
                dbContextName));

        services.AddOutboxJobs<TDbContext>(configuration);
        
        return services;
    }
}
