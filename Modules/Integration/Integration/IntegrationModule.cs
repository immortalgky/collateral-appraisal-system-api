using Integration.Application.Services;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.Contracts.FileSource;
using Integration.Domain.IdempotencyRecords;
using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.FileInterface.Format.CollateralResult;
using Integration.FileInterface.Format.Reappraisal;
using Integration.FileInterface.Format.RegulatoryExport;
using Integration.FileInterface.Jobs.CollateralResult;
using Integration.FileInterface.Jobs.Reappraisal;
using Integration.FileInterface.Jobs.RegulatoryExport;
using Integration.Infrastructure;
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
using Request.Application.Services;
using Shared.Data;
using Shared.Data.Extensions;

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

        // Format utilities (moved from Collateral).
        services.AddSingleton<CollatrevFileParser>();
        services.AddSingleton<CollatrevFileWriter>();
        services.AddScoped<CollatrevTestFileBuilder>();
        services.AddSingleton<CollateralResultFileWriter>();
        services.AddSingleton<RegulatoryFileWriter>();

        // File interface jobs (moved from Collateral — thin orchestration only).
        services.AddScoped<As400ReappraisalJob>();
        services.AddScoped<CollateralResultExportJob>();
        services.AddScoped<RegulatoryExportJob>();

        // Register services
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IAppraisalLookupService, AppraisalLookupService>();
        services.AddScoped<IQuotationFinalizeLookupService, QuotationFinalizeLookupService>();
        services.AddTransient<IUpdateRequestService, UpdateRequestService>();
        services.AddTransient<WebhookAttemptCounterHandler>();
        var webhookClientBuilder = services.AddHttpClient("Webhook");
        webhookClientBuilder.AddStandardResilienceHandler();
        webhookClientBuilder.AddHttpMessageHandler<WebhookAttemptCounterHandler>();

        // Register unit of work
        services.AddScoped<IIntegrationUnitOfWork>(sp =>
            new IntegrationUnitOfWork(sp.GetRequiredService<IntegrationDbContext>(), sp));

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

    public static IApplicationBuilder UseIntegrationModule(this IApplicationBuilder app)
    {
        app.UseMigration<IntegrationDbContext>();
        return app;
    }
}
