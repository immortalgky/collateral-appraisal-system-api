using Appraisal.Application.Services;
using Appraisal.Domain.ComparativeAnalysis;
using Appraisal.Infrastructure.Repositories;
using Appraisal.Infrastructure.Seed;
using Shared.Data.Seed;

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

        // Register Document Requirement repository
        services.AddScoped<IDocumentRequirementRepository, DocumentRequirementRepository>();

        // Register Application Services
        services.AddScoped<IAppraisalCreationService, AppraisalCreationService>();

        // Register Data Seeder
        services.AddScoped<IDataSeeder<AppraisalDbContext>, DocumentRequirementDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseAppraisalModule(this IApplicationBuilder app)
    {
        //app.UseMigration<AppraisalDbContext>();
        return app;
    }
}