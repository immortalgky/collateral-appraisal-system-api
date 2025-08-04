using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Security;
using Shared.Time;

namespace Shared.Extensions;

/// <summary>
/// Extension methods for registering shared services
/// </summary>
public static class SharedServicesExtensions
{
    /// <summary>
    /// Registers shared infrastructure services
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure timezone and culture settings
        services.Configure<TimeZoneConfiguration>(
            configuration.GetSection(TimeZoneConfiguration.SectionName));

        // Time abstraction
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Security services
        services.AddSingleton<ICertificateProvider, CertificateProvider>();

        return services;
    }
}