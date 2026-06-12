using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.Data.Interceptors;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Application.Providers;
using Reporting.Data;
using Reporting.Application.Services;
using Reporting.Infrastructure;
using Reporting.Infrastructure.BrowserPool;
using Reporting.Infrastructure.PdfAssembly;
using Reporting.Infrastructure.Rendering;
using Reporting.Infrastructure.Templates;

namespace Reporting;

/// <summary>
/// Module registration for the Reporting subsystem.
/// Call <see cref="AddReportingModule"/> from Program.cs during service configuration
/// and <see cref="UseReportingModule"/> during pipeline configuration.
/// </summary>
public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PdfSharp 6.x requires CodePages encoding provider for Windows-1252 PDFs.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Configuration
        services.Configure<ReportingConfiguration>(
            configuration.GetSection(ReportingConfiguration.SectionName));

        // Pipeline services
        services.AddTransient<ReportGenerationService>();
        services.AddTransient<Application.Services.IReportEntityResolver, Infrastructure.ReportEntityResolver>();
        services.AddTransient<ITemplateStore, FileTemplateStore>();
        services.AddTransient<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddTransient<IPdfRenderer, PuppeteerPdfRenderer>();
        services.AddTransient<IPdfAssembler, PdfSharpAssembler>();
        services.AddTransient<IReportAttachmentSource, DapperAttachmentSource>();

        // Generic tabular file builder (Excel/CSV via ClosedXML, PDF via the Puppeteer pool).
        // Exposed cross-module through Reporting.Contracts.ITabularExporter.
        services.AddTransient<Reporting.Contracts.ITabularExporter, Infrastructure.Export.TabularExporter>();

        // Inline PDF generation port — consumed by Notification module's ReportAttachmentResolver
        // to auto-attach generated report PDFs to outbound emails.
        services.AddTransient<Reporting.Contracts.IReportPdfGenerator, Infrastructure.ReportPdfGeneratorService>();

        // Operational reports (FSD Ch.9): one generic runner executes every report definition
        // as a paginated preview or an Excel/CSV/PDF export.
        services.AddScoped<Application.OperationalReports.Shared.IOperationalReportRunner,
            Application.OperationalReports.Shared.OperationalReportRunner>();

        // OLA timing: reads workflow CompletedTasks + computes business-time segments
        // (reuses Workflow.Contracts.Sla.IBusinessTimeCalculator, implemented by the Workflow module).
        services.AddScoped<Application.OperationalReports.Shared.IOlaTimingService,
            Application.OperationalReports.Shared.OlaTimingService>();

        // SaveChanges interceptors: AuditableEntityInterceptor (no-op for Reporting's non-IEntity
        // tables) + DispatchDomainEventInterceptor, which drains the scoped IOutboxScope into the
        // DbContext so outbox rows commit atomically with the ReportJobs status update.
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        // Write-side: report-generation audit log, definitions, jobs, and the integration-event outbox.
        services.AddDbContext<ReportingDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure();
                sqlOptions.MigrationsAssembly(typeof(ReportingDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "reporting");
            });
        });
        services.AddScoped<IReportAuditLogger, ReportAuditLogger>();

        // Data providers — add one per report type
        services.AddTransient<IReportDataProvider, AppointmentQuotationDataProvider>();
        // Unified entry point — composite: dispatches to the per-property summary forms below.
        services.AddTransient<IReportDataProvider, AppraisalSummaryDataProvider>();
        services.AddTransient<IReportDataProvider, AppraisalSummaryLandBuildingDataProvider>();
        services.AddTransient<IReportDataProvider, AppraisalSummaryCondoDataProvider>();
        services.AddTransient<IReportDataProvider, AppraisalSummaryMachineDataProvider>();
        services.AddTransient<IReportDataProvider, AppraisalSummaryConstructionDataProvider>();
        services.AddTransient<IReportDataProvider, AppraisalSummaryBlockDataProvider>();
        // Unified appraisal book — auto-detects internal/external + body type (replaces the former
        // external-appraisal-report / internal-report-construction / internal-report-block).
        services.AddTransient<IReportDataProvider, AppraisalBookDataProvider>();
        services.AddTransient<IReportDataProvider, MeetingInvitationDataProvider>();
        services.AddTransient<IReportDataProvider, MeetingMinuteDataProvider>();

        // In-memory cache for the report-definition config table (60s TTL per node).
        // AddMemoryCache is idempotent — safe to call even if another module already called it.
        services.AddMemoryCache();

        // Registry (resolved from all registered IReportDataProvider instances + DB config).
        // Scoped — NOT singleton — because the providers depend on the scoped
        // ISqlConnectionFactory; a singleton registry would capture that scoped
        // dependency (captive-dependency / lifetime-validation error).
        services.AddScoped<IReportRegistry, ReportRegistry>();

        // Browser pool: singleton + warm-up hosted service
        services.AddSingleton<PuppeteerBrowserPool>();
        services.AddSingleton<IBrowserPool>(sp => sp.GetRequiredService<PuppeteerBrowserPool>());
        services.AddHostedService(sp => sp.GetRequiredService<PuppeteerBrowserPool>());

        // Phase B: async PDF generation jobs.
        // Transient: Hangfire creates a DI scope per execution and resolves the job class
        // within it, so transient lifetime is correct here.
        services.AddTransient<ReportGenerationJob>();
        services.AddTransient<ReportArtifactCleanupJob>();

        return services;
    }

    /// <summary>
    /// Applies the module's EF migrations at startup (ReportGenerationLogs / ReportDefinitions /
    /// ReportJobs + the integration-event outbox), mirroring every other module's UseXModule. This
    /// must run so the IntegrationEventDeliveryService&lt;ReportingDbContext&gt; finds its outbox +
    /// BackgroundServiceLease tables.
    /// </summary>
    public static IApplicationBuilder UseReportingModule(this IApplicationBuilder app)
    {
        app.UseMigration<ReportingDbContext>();
        return app;
    }
}
