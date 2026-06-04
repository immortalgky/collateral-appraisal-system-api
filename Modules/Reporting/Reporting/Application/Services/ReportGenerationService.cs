namespace Reporting.Application.Services;

/// <summary>
/// Orchestrates the full report generation pipeline:
/// Registry → DataProvider → TemplateStore → TemplateRenderer → PdfAssembler → bytes.
/// </summary>
public sealed class ReportGenerationService(
    IReportRegistry registry,
    ITemplateStore templateStore,
    ITemplateRenderer renderer,
    IPdfAssembler assembler,
    ILogger<ReportGenerationService> logger)
{
    public async Task<byte[]> GenerateAsync(
        string reportTypeKey,
        string entityId,
        CancellationToken cancellationToken)
    {
        var registration = registry.TryGet(reportTypeKey)
            ?? throw new NotFoundException(nameof(reportTypeKey), reportTypeKey);

        logger.LogInformation(
            "Generating report {ReportTypeKey} for entity {EntityId}",
            reportTypeKey, entityId);

        // [1] Resolve data
        var model = await registration.Provider.GetModelAsync(entityId, cancellationToken);

        // [2] Load template
        var templateHtml = await templateStore.GetTemplateAsync(
            registration.TemplateId, cancellationToken);

        // [3] Render HTML → ordered segments (splits at <!-- SLOT: name --> markers)
        var segments = await renderer.RenderAsync(templateHtml, model, cancellationToken);

        // [4] Assemble segments (HTML→PDF per fragment, merge attachment PDFs)
        var pdfBytes = await assembler.AssembleAsync(segments, cancellationToken);

        logger.LogInformation(
            "Report {ReportTypeKey} generated: {Bytes} bytes",
            reportTypeKey, pdfBytes.Length);

        return pdfBytes;
    }
}
