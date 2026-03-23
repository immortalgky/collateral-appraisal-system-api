using Appraisal.Application.Services;
using Appraisal.Domain.ComparativeAnalysis;
using Appraisal.Domain.Services;
using Appraisal.Infrastructure.Repositories;
using Appraisal.Infrastructure.Seed;
// DocumentRequirement entities moved to Parameter module
using Shared.Data.Seed;
using Shared.Messaging.Services;

namespace Appraisal;

public static class AppraisalModule
{
    public static IServiceCollection AddAppraisalModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<AppraisalDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppraisalDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "appraisal");
            });
        });

        // Register Unit of Work
        services.AddScoped<IAppraisalUnitOfWork, AppraisalUnitOfWork>();

        // Register Aggregate Repositories (only aggregates have repositories)
        services.AddScoped<IAppraisalRepository, AppraisalRepository>();
        services.AddScoped<IPricingAnalysisRepository, PricingAnalysisRepository>();
        services.AddScoped<ICommitteeRepository, CommitteeRepository>();
        services.AddScoped<IMarketComparableRepository, MarketComparableRepository>();
        services.AddScoped<IAppraisalSettingsRepository, AppraisalSettingsRepository>();
        services.AddScoped<IAutoAssignmentRuleRepository, AutoAssignmentRuleRepository>();

        // Register Market Comparable Template repositories
        services.AddScoped<IMarketComparableTemplateRepository, MarketComparableTemplateRepository>();
        services.AddScoped<IMarketComparableFactorRepository, MarketComparableFactorRepository>();

        // Register Comparative Analysis Template repository
        services.AddScoped<IComparativeAnalysisTemplateRepository, ComparativeAnalysisTemplateRepository>();

        // Register additional aggregate repositories
        services.AddScoped<IQuotationRepository, QuotationRepository>();

        // Register Gallery repository
        services.AddScoped<IAppraisalGalleryRepository, AppraisalGalleryRepository>();

        // Register Photo Topic repository
        services.AddScoped<IPhotoTopicRepository, PhotoTopicRepository>();

        // Register Law and Regulation repository
        services.AddScoped<ILawAndRegulationRepository, LawAndRegulationRepository>();

        // Register Appendix repository
        services.AddScoped<IAppraisalAppendixRepository, AppraisalAppendixRepository>();

        // Register Decision repository
        services.AddScoped<IAppraisalDecisionRepository, AppraisalDecisionRepository>();

        // Register Application Services
        services.AddScoped<IAppraisalCreationService, AppraisalCreationService>();
        services.AddScoped<IAppraisalStatusService, AppraisalStatusService>();

        // Register Domain Services
        services.AddSingleton<PricingCalculationServiceResolver>();

        // Register Data Seeders
        services.AddScoped<IDataSeeder<AppraisalDbContext>, AppendixTypeDataSeed>();
        services.AddScoped<IDataSeeder<AppraisalDbContext>, CommitteeThresholdDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseAppraisalModule(this IApplicationBuilder app)
    {
        app.UseMigration<AppraisalDbContext>();
        return app;
    }
}