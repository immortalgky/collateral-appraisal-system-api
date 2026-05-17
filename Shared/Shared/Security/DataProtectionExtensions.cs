using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Security;

public static class DataProtectionExtensions
{
    private const string ApplicationName = "CollateralAppraisalSystem";

    public static IServiceCollection AddSharedDataProtection<TContext>(this IServiceCollection services)
        where TContext : DbContext, IDataProtectionKeyContext
    {
        services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<TContext>()
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

        return services;
    }
}
