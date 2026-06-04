using System.Text;
using Reporting.Application.Providers;
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
        services.AddTransient<ITemplateStore, FileTemplateStore>();
        services.AddTransient<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddTransient<IPdfRenderer, PuppeteerPdfRenderer>();
        services.AddTransient<IPdfAssembler, PdfSharpAssembler>();
        services.AddTransient<IReportAttachmentSource, DapperAttachmentSource>();

        // Data providers — add one per report type
        services.AddTransient<IReportDataProvider, AppointmentQuotationDataProvider>();

        // Registry (resolved from all registered IReportDataProvider instances).
        // Scoped — NOT singleton — because the providers depend on the scoped
        // ISqlConnectionFactory; a singleton registry would capture that scoped
        // dependency (captive-dependency / lifetime-validation error).
        services.AddScoped<IReportRegistry, ReportRegistry>(sp =>
            new ReportRegistry(sp.GetServices<IReportDataProvider>()));

        // Browser pool: singleton + warm-up hosted service
        services.AddSingleton<PuppeteerBrowserPool>();
        services.AddSingleton<IBrowserPool>(sp => sp.GetRequiredService<PuppeteerBrowserPool>());
        services.AddHostedService(sp => sp.GetRequiredService<PuppeteerBrowserPool>());

        return services;
    }

    /// <summary>
    /// No middleware required for v1 (no DbContext migrations to run).
    /// Kept for consistency with other modules.
    /// </summary>
    public static IApplicationBuilder UseReportingModule(this IApplicationBuilder app)
    {
        return app;
    }
}
