using Microsoft.EntityFrameworkCore;
using Notification.Data;
using Notification.Data.Repository;
using Notification.Data.Seed;
using Notification.Domain.Notifications.Hubs;
using Notification.Domain.Notifications.Services;
using Shared.Data.Extensions;
using Shared.Data.Seed;
using StackExchange.Redis;

namespace Notification;

public static class NotificationModule
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register Entity Framework DbContext
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "notification");
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            }));

        // Register SignalR (covers all hubs in the app; WorkflowModule maps its hub on this registration)
        var signalR = services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        });

        if (configuration.GetValue("SignalR:UseRedisBackplane", false))
        {
            signalR.AddStackExchangeRedis(
                configuration.GetConnectionString("Redis")!,
                redisOptions =>
                {
                    redisOptions.Configuration.ChannelPrefix =
                        RedisChannel.Literal(configuration["SignalR:ChannelPrefix"] ?? "cas");
                    redisOptions.Configuration.AbortOnConnectFail = false;
                });
        }

        // Register notification services
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();

        // Register data seeder
        services.AddScoped<IDataSeeder<NotificationDbContext>, NotificationDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseNotificationModule(this IApplicationBuilder app)
    {
        // Map SignalR hub
        app.UseRouting();
        app.UseEndpoints(endpoints => { endpoints.MapHub<NotificationHub>("/notificationHub"); });

        app.UseMigration<NotificationDbContext>();

        return app;
    }
}