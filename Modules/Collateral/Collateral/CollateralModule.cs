using Collateral.CollateralMasters.As400Legacy;
using Collateral.CollateralMasters.CollateralResult;
using Collateral.CollateralMasters.HostLink;
using Collateral.CollateralMasters.Reappraisal;
using Collateral.CollateralMasters.Reappraisal.Services;
using Collateral.CollateralMasters.RegulatoryExport;
using Collateral.Contracts.As400Legacy;
using Collateral.Contracts.FileInterface;
using Collateral.Contracts.HostLink;
using Collateral.Contracts.Reappraisal;
using Shared.Data.Interceptors;
using Shared.Scheduling;
using Collateral.Scheduling;

namespace Collateral;

public static class CollateralModule
{
    public static IServiceCollection AddCollateralModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        services.AddDbContext<CollateralDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(CollateralDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "collateral");
            });
        });

        services.AddScoped<ICollateralMasterRepository, CollateralMasterRepository>();
        services.AddScoped<ICollateralMasterUpsertService, CollateralMasterUpsertService>();

        // Singleton: in-memory job state must survive across requests
        services.AddSingleton<CollateralBackfillJob>();
        services.AddSingleton<HostCollateralIdBackfillJob>();

        // Scoped: Hangfire instantiates per execution via DI scope
        services.AddScoped<BlockReappraisalJob>();

        // Reappraisal group number generator (stays in Collateral — pure data).
        services.AddScoped<IReappraisalGroupNumberGenerator, ReappraisalGroupNumberGenerator>();

        // Collateral data impls exposed via Collateral.Contracts interfaces.
        // Query implementations: IInboundFileSource is registered by IntegrationModule (config-switched).
        services.AddScoped<IRegulatoryExportQuery, RegulatoryExportQuery>();
        services.AddScoped<ICollateralResultQuery, CollateralResultQuery>();
        services.AddScoped<ICollateralResultLedger, CollateralResultLedger>();
        services.AddScoped<IReappraisalIngestor, ReappraisalIngestor>();
        services.AddScoped<IHostCollateralLinkIngestor, HostCollateralLinkIngestor>();
        services.AddScoped<IAs400LegacyImporter, As400LegacyImporter>();

        return services;
    }

    public static IApplicationBuilder UseCollateralModule(this IApplicationBuilder app)
    {
        app.UseDataSeeding<CollateralDbContext>();
        app.UseModuleRecurringJobs<CollateralDbContext>(CollateralRecurringJobs.All);

        return app;
    }
}
