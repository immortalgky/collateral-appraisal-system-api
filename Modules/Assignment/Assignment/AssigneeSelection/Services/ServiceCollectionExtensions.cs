namespace Assignment.AssigneeSelection.Services;

/// <summary>
/// Extension methods for registering custom assignment services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Builder for configuring custom assignment services
    /// </summary>
    public class CustomAssignmentServiceBuilder
    {
        private readonly IServiceCollection _services;
        private readonly List<(string Name, Type ServiceType)> _serviceRegistrations = new();

        internal CustomAssignmentServiceBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Registers a custom assignment service
        /// </summary>
        /// <typeparam name="TService">The custom assignment service type</typeparam>
        /// <param name="serviceName">Optional custom name for the service (defaults to class name)</param>
        /// <returns>The builder for method chaining</returns>
        public CustomAssignmentServiceBuilder AddService<TService>(string? serviceName = null)
            where TService : class, ICustomAssignmentService
        {
            // Register the service in DI container
            _services.AddScoped<TService>();

            // Store registration for later factory setup
            var name = serviceName ?? typeof(TService).Name;
            _serviceRegistrations.Add((name, typeof(TService)));

            return this;
        }

        /// <summary>
        /// Completes the builder configuration and registers the factory
        /// </summary>
        /// <returns>The service collection</returns>
        public IServiceCollection Build()
        {
            // Register factory with all service registrations
            _services.AddSingleton<ICustomAssignmentServiceFactory>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<CustomAssignmentServiceFactory>>();
                var factory = new CustomAssignmentServiceFactory(serviceProvider, logger);

                // Register all services with the factory
                foreach (var (name, serviceType) in _serviceRegistrations)
                {
                    factory.GetType()
                        .GetMethod(nameof(CustomAssignmentServiceFactory.RegisterService), 
                            new[] { typeof(string) })!
                        .MakeGenericMethod(serviceType)
                        .Invoke(factory, new object[] { name });
                }

                return factory;
            });

            return _services;
        }
    }

    /// <summary>
    /// Adds custom assignment service support to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>Builder for configuring custom assignment services</returns>
    public static CustomAssignmentServiceBuilder AddCustomAssignmentServices(this IServiceCollection services)
    {
        return new CustomAssignmentServiceBuilder(services);
    }
}