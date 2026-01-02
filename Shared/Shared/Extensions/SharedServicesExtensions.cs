using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configurations;
using Shared.Identity;
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

        // Configure file storage settings
        services.Configure<FileStorageConfiguration>(
            configuration.GetSection(FileStorageConfiguration.SectionName));

        // Time abstraction
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Security services
        services.AddSingleton<ICertificateProvider, CertificateProvider>();

        // Identity services
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}