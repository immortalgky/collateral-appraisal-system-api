using System.Reflection;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Messaging.OutboxPatterns.Wrappers;

namespace Shared.Messaging.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddMassTransitWithAssemblies(this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        services.AddMassTransit(config =>
        {
            config.SetKebabCaseEndpointNameFormatter();

            config.SetInMemorySagaRepositoryProvider();

            // Register consumers from assemblies first
            config.AddConsumers(assemblies);

            // Register wrapper consumers last (higher priority for Inbox modules)
            var wrappers = ConsumerWrapperRegistry.GetRegistered();
            if (wrappers.Count > 0)
                config.AddConsumers(wrappers.ToArray());

            config.AddSagaStateMachines(assemblies);
            config.AddSagas(assemblies);
            config.AddActivities(assemblies);

            config.UsingRabbitMq((context, configurator) =>
            {
                configurator.Host(new Uri(configuration["RabbitMQ:Host"]!), host =>
                {
                    host.Username(configuration["RabbitMQ:Username"]!);
                    host.Password(configuration["RabbitMQ:Password"]!);

                    configurator.ConfigureEndpoints(context);
                });
            });
        });

        return services;
    }
}