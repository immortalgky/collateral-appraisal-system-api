namespace Assignment.AssigneeSelection.Services;

/// <summary>
/// Factory implementation for creating and resolving custom assignment services
/// Uses service provider for dependency injection and service resolution
/// </summary>
public class CustomAssignmentServiceFactory : ICustomAssignmentServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _serviceRegistry;
    private readonly ILogger<CustomAssignmentServiceFactory> _logger;

    public CustomAssignmentServiceFactory(
        IServiceProvider serviceProvider,
        ILogger<CustomAssignmentServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _serviceRegistry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registers a custom assignment service type with a name
    /// </summary>
    /// <typeparam name="TService">The service type</typeparam>
    /// <param name="serviceName">The name to register the service under</param>
    public void RegisterService<TService>(string serviceName) 
        where TService : class, ICustomAssignmentService
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

        _serviceRegistry[serviceName] = typeof(TService);
        _logger.LogDebug("Registered custom assignment service: {ServiceName} -> {ServiceType}", 
            serviceName, typeof(TService).Name);
    }

    /// <summary>
    /// Registers a custom assignment service type using its class name as the service name
    /// </summary>
    /// <typeparam name="TService">The service type</typeparam>
    public void RegisterService<TService>() 
        where TService : class, ICustomAssignmentService
    {
        var serviceName = typeof(TService).Name;
        RegisterService<TService>(serviceName);
    }

    public ICustomAssignmentService? GetService(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogWarning("Attempted to get custom assignment service with null or empty name");
            return null;
        }

        if (!_serviceRegistry.TryGetValue(serviceName, out var serviceType))
        {
            _logger.LogWarning("Custom assignment service '{ServiceName}' not found in registry. Available services: {AvailableServices}",
                serviceName, string.Join(", ", _serviceRegistry.Keys));
            return null;
        }

        try
        {
            var service = _serviceProvider.GetService(serviceType) as ICustomAssignmentService;
            if (service == null)
            {
                _logger.LogError("Failed to resolve custom assignment service '{ServiceName}' of type '{ServiceType}' from service provider",
                    serviceName, serviceType.Name);
                return null;
            }

            _logger.LogDebug("Successfully resolved custom assignment service: {ServiceName}", serviceName);
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving custom assignment service '{ServiceName}' of type '{ServiceType}'",
                serviceName, serviceType.Name);
            return null;
        }
    }

    public bool HasService(string serviceName)
    {
        return !string.IsNullOrWhiteSpace(serviceName) && 
               _serviceRegistry.ContainsKey(serviceName);
    }

    public IEnumerable<string> GetAvailableServices()
    {
        return _serviceRegistry.Keys.ToList();
    }
}