using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common;

public static class CommonModule
{
    public static IServiceCollection AddCommonModule(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static IApplicationBuilder UseCommonModule(this IApplicationBuilder app)
    {
        return app;
    }
}
