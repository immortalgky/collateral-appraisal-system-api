using System.Net;
using Integration.Application.Services;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.Contracts.FileSource;
using Integration.FailedMessages;
using Integration.Domain.IdempotencyRecords;
using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.FileInterface.Format.CollateralResult;
using Integration.FileInterface.Format.HostLink;
using Integration.FileInterface.Format.Reappraisal;
using Integration.FileInterface.Format.RegulatoryExport;
using Integration.FileInterface.Jobs.CollateralResult;
using Integration.FileInterface.Jobs.HostLink;
using Integration.FileInterface.Jobs.Reappraisal;
using Integration.FileInterface.Jobs.RegulatoryExport;
using Integration.Infrastructure;
using Collateral.Contracts.FileInterface;
using Integration.Infrastructure.FileInterface;
using Integration.Infrastructure.FileSink;
using Integration.Infrastructure.FileSource;
using Integration.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Request.Application.Services;
using Shared.Data;
using Shared.Security;
using Shared.Data.Extensions;
using Shared.Scheduling;
using Integration.Scheduling;

public static class IntegrationModule
{
    public static IServiceCollection AddIntegrationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register repositories
        services.AddScoped<IWebhookSubscriptionRepository, WebhookSubscriptionRepository>();
        services.AddScoped<IRepository<WebhookSubscription, Guid>, WebhookSubscriptionRepository>();

        services.AddScoped<IIdempotencyRecordRepository, IdempotencyRecordRepository>();
        services.AddScoped<IRepository<IdempotencyRecord, Guid>, IdempotencyRecordRepository>();

        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IRepository<WebhookDelivery, Guid>, WebhookDeliveryRepository>();

        // Outbound file sink (port in Integration.Contracts; impl is config-switched).
        // Slimmed options under FileTransfer:Outbound (non-secret; paths come from DB config).
        services.Configure<OutboundFileSinkOptions>(
            configuration.GetSection(OutboundFileSinkOptions.SectionName));
        var sinkType = configuration.GetValue<string>($"{OutboundFileSinkOptions.SectionName}:FileSource");
        if (FileTransferTransport.IsSftp(sinkType))
            services.AddScoped<IOutboundFileSink, SftpFileSink>();
        else
            services.AddScoped<IOutboundFileSink, LocalFileSink>();

        // ...and the filesystem adapter again, under a key, so a job can reach it even where the
        // default above is SFTP. Additive: the unkeyed registration is untouched and every existing
        // caller still gets the config-switched sink. Only RegulatoryExportJob asks for this one,
        // for the Excel companion that belongs on a Windows share rather than AS400's SFTP drop.
        services.AddKeyedScoped<IOutboundFileSink, LocalFileSink>(OutboundFileSinkKeys.FileSystem);

        // Inbound file source (port in Integration.Contracts; impl is config-switched).
        // Slimmed options under FileTransfer:Inbound (non-secret; paths come from DB config).
        services.Configure<InboundFileSourceOptions>(
            configuration.GetSection(InboundFileSourceOptions.SectionName));
        var inboundSourceType = configuration
            .GetSection(InboundFileSourceOptions.SectionName)
            .GetValue<string>("FileSource");
        if (FileTransferTransport.IsSftp(inboundSourceType))
            services.AddScoped<IInboundFileSource, SftpInboundFileSource>();
        else
            services.AddScoped<IInboundFileSource, LocalInboundFileSource>();

        // DB-driven file interface config provider (60s TTL cache).
        services.AddScoped<IFileInterfaceConfigProvider, FileInterfaceConfigProvider>();

        // Decides which inbound files still need work and records the outcome. Shared by both AS400
        // ingest jobs so the de-duplication rule cannot drift between them.
        services.AddScoped<InboundFileLedger>();
        services.AddScoped<InboundFileRunner>();

        // Outbound Collateral Result. Implements a Collateral.Contracts interface from here because
        // the query is an interface concern: it reads the appraisal schema and the AS400 link table,
        // and needs nothing from the Collateral module.
        services.AddScoped<ICollateralResultQuery, CollateralResultExportQuery>();

        // Format utilities (moved from Collateral).
        services.AddSingleton<CollatrevFileParser>();
        services.AddSingleton<CollatrevFileWriter>();
        services.AddScoped<CollatrevTestFileBuilder>();
        services.AddSingleton<HostCollateralLinkFileParser>();
        services.AddSingleton<CollateralResultFileWriter>();
        services.AddSingleton<RegulatoryFileWriter>();
        services.AddSingleton<RegulatoryExcelWriter>();

        // File interface jobs (moved from Collateral — thin orchestration only).
        services.AddScoped<As400ReappraisalJob>();
        services.AddScoped<As400HostLinkJob>();
        // Singleton, unlike the scheduled jobs: it hands the caller a job id and keeps the outcome
        // in memory for polling, so the instance must outlive the request — same as
        // CollateralBackfillJob. It opens its own scope for the actual work.
        services.AddSingleton<As400LegacyImportJob>();
        services.AddScoped<CollateralResultExportJob>();
        services.AddScoped<RegulatoryExportJob>();

        // Register services
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IWebhookTokenProvider, LosTokenProvider>();
        // Encrypts/decrypts WebhookSubscriptions.SecretKey / ClientSecret with the secrets cert.
        services.AddSingleton<ColumnSecretCipher>();
        services.AddScoped<IAppraisalLookupService, AppraisalLookupService>();
        services.AddScoped<IQuotationFinalizeLookupService, QuotationFinalizeLookupService>();
        services.AddTransient<IUpdateRequestService, UpdateRequestService>();
        services.AddTransient<WebhookAttemptCounterHandler>();
        var webhookClientBuilder = services.AddHttpClient("Webhook");
        webhookClientBuilder.AddStandardResilienceHandler();
        webhookClientBuilder.AddHttpMessageHandler<WebhookAttemptCounterHandler>();

        // Token-fetch client for TokenBearer webhook subscriptions (e.g. LOS). Separate named
        // client from "Webhook" so the token endpoint's resilience/retry policy is independent of
        // the update-callback policy.
        services.AddHttpClient(LosTokenProvider.HttpClientName).AddStandardResilienceHandler();

        // AddMemoryCache is idempotent — safe to call even though Auth/Reporting/Common/Parameter
        // modules already register it. Backs IWebhookTokenProvider's per-subscription token cache.
        services.AddMemoryCache();

        // Register unit of work
        services.AddScoped<IIntegrationUnitOfWork>(sp =>
            new IntegrationUnitOfWork(sp.GetRequiredService<IntegrationDbContext>(), sp));

        // Failed-messages collector (docs/failed-messages/design.md §3) — per-node BackgroundService,
        // only registered when enabled, reading the app's own RabbitMQ credentials/host.
        // Same ValidateOnStart/PostConfigure pattern as BackgroundJobsOptions
        // (Shared/Shared/Extensions/SharedServicesExtensions.cs) — forces Validate() to run during host
        // startup so a bad cadence fails fast instead of on first resolution. Validate() is a no-op while
        // FailedMessages:Enabled is false, so a disabled collector never blocks startup.
        services.AddOptions<FailedMessagesOptions>()
            .Bind(configuration.GetSection(FailedMessagesOptions.SectionName))
            // The management API is the same broker as RabbitMQ:Host, so its URL lives with the other
            // broker settings. Assigned unconditionally (blank = default); the old FailedMessages key is never
            // read, only flagged so Validate() can reject it.
            .Configure(o =>
            {
                var url = configuration["RabbitMQ:ManagementUrl"];
                o.ManagementUrl = string.IsNullOrWhiteSpace(url) ? FailedMessagesOptions.DefaultManagementUrl : url;
                o.LegacyManagementUrlPresent = configuration["FailedMessages:ManagementUrl"] is not null;
            })
            .ValidateOnStart();
        services.PostConfigure<FailedMessagesOptions>(o => o.Validate());
        AddManagementHttpClient(services);
        if (configuration.GetValue<bool>($"{FailedMessagesOptions.SectionName}:Enabled"))
            services.AddHostedService<FailedMessageCollectorService>();

        // Register DbContext
        services.AddDbContext<IntegrationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(IntegrationDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "integration");
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            });
        });

        return services;
    }

    /// <summary>The collector's Management API client. Internal so a unit test can inspect its handler.</summary>
    internal static void AddManagementHttpClient(IServiceCollection services)
    {
        services.AddHttpClient(FailedMessageCollectorService.ManagementHttpClientName, (sp, client) =>
        {
            var managementUrl = sp.GetRequiredService<IOptions<FailedMessagesOptions>>().Value.ManagementUrl;
            // HttpClient/Uri only APPENDS a relative request path onto BaseAddress's own
            // path when BaseAddress ends with '/' — otherwise the last path segment (or, if ManagementUrl
            // has no path at all, silently nothing) is dropped. A trailing slash here is what lets a
            // ManagementUrl with a path prefix (e.g. "http://host:15672/rabbit") still work once paired
            // with the collector's now-relative (no leading '/') request paths.
            client.BaseAddress = new Uri(managementUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        // gzip/deflate: the projected response is ~169 KB per round per node, ~5 KB compressed.
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
    }

    public static IApplicationBuilder UseIntegrationModule(this IApplicationBuilder app)
    {
        app.UseDataSeeding<IntegrationDbContext>();
        app.UseModuleRecurringJobs<IntegrationDbContext>(IntegrationRecurringJobs.All);
        return app;
    }
}
