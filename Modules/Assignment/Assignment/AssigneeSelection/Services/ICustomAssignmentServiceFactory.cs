namespace Assignment.AssigneeSelection.Services;

/// <summary>
/// Factory for creating and resolving custom assignment services
/// </summary>
public interface ICustomAssignmentServiceFactory
{
    /// <summary>
    /// Gets a custom assignment service by name
    /// </summary>
    /// <param name="serviceName">The name of the service to retrieve</param>
    /// <returns>The custom assignment service instance, or null if not found</returns>
    ICustomAssignmentService? GetService(string serviceName);

    /// <summary>
    /// Checks if a custom assignment service exists with the given name
    /// </summary>
    /// <param name="serviceName">The name of the service to check</param>
    /// <returns>True if the service exists, false otherwise</returns>
    bool HasService(string serviceName);

    /// <summary>
    /// Gets all available custom assignment service names
    /// </summary>
    /// <returns>Collection of service names</returns>
    IEnumerable<string> GetAvailableServices();
}