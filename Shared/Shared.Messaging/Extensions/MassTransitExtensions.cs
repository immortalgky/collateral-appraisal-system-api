using System.Collections.Generic;
using System.Linq;
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
        var wrapperTypes = ConsumerWrapperRegistry.GetRegistered();
        var wrappedConsumerTypes = ConsumerWrapperRegistry.GetWrappedConsumers();
        var wrappedConsumerSet = new HashSet<Type>(wrappedConsumerTypes);

        foreach (var consumerType in wrappedConsumerSet)
        {
            services.AddScoped(consumerType);
        }

        var consumerTypes = assemblies
            .SelectMany(GetConsumerTypes)
            .Where(type => !wrappedConsumerSet.Contains(type))
            .Distinct()
            .ToArray();

        services.AddMassTransit(config =>
        {
            config.SetKebabCaseEndpointNameFormatter();

            config.SetInMemorySagaRepositoryProvider();

            if (consumerTypes.Length > 0)
                config.AddConsumers(consumerTypes);

            if (wrapperTypes.Count > 0)
                config.AddConsumers(wrapperTypes.ToArray());

            config.AddSagaStateMachines(assemblies);
            config.AddSagas(assemblies);
            config.AddActivities(assemblies);

            config.UsingRabbitMq((context, configurator) =>
            {
                configurator.Host(new Uri(configuration["RabbitMQ:Host"]!), host =>
                {
                    host.Username(configuration["RabbitMQ:Username"]!);
                    host.Password(configuration["RabbitMQ:Password"]!);
                });

                configurator.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    private static IEnumerable<Type> GetConsumerTypes(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(type => type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)));
}