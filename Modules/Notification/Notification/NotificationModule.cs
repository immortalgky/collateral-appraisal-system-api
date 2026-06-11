using Microsoft.EntityFrameworkCore;
using Notification.Contracts.Email;
using Notification.Contracts.Realtime;
using Notification.Data;
using Notification.Data.Repository;
using Notification.Data.Seed;
using Notification.Domain.Notifications.Hubs;
using Notification.Domain.Notifications.Services;
using Notification.Infrastructure.Email;
using Notification.Infrastructure.Email.Attachments;
using Notification.Infrastructure.Email.Templates;
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
        services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();

        // Register data seeder
        services.AddScoped<IDataSeeder<NotificationDbContext>, NotificationDataSeed>();

        // ── Email infrastructure ──────────────────────────────────────────────────
        services.Configure<MailConfiguration>(configuration.GetSection(MailConfiguration.SectionName));
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        services.AddTransient<IEmailTemplateRenderer, EmailTemplateRenderer>();

        // Attachment resolvers (dispatched by type discriminator via EmailAttachmentAssembler).
        // Add more resolver types here as new attachment sources are introduced.
        services.AddTransient<IEmailAttachmentResolver, DocumentAttachmentResolver>();
        services.AddTransient<IEmailAttachmentResolver, ReportAttachmentResolver>();
        services.AddTransient<EmailAttachmentAssembler>();

        // Send-log writer: best-effort persistence of every email attempt outcome.
        services.AddScoped<IEmailSendLogWriter, EmailSendLogWriter>();

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