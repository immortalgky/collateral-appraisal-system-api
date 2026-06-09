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

        // A disabled report is the operator's no-redeploy kill-switch. Honour it on the
        // synchronous path too (the async enqueue endpoint already refuses disabled reports),
        // so GET /reports/{key}/{id} cannot render a report an operator has switched off.
        if (!registration.IsEnabled)
            throw new NotFoundException(nameof(reportTypeKey), reportTypeKey);

        logger.LogInformation(
            "Generating report {ReportTypeKey} for entity {EntityId}",
            reportTypeKey, entityId);

        // [0] Composite reports (e.g. unified "appraisal-summary") resolve to a set of child
        // report keys whose PDFs are rendered through this same pipeline and concatenated.
        // The children are NOT seeded in ReportDefinitions; they resolve via ReportRegistry's
        // provider fallback (registered IReportDataProvider, no DB row → IsEnabled=true,
        // TemplateId==key), so the recursive call's IsEnabled check passes. Each child is
        // rendered inline/synchronously here regardless of its resolved GenerationMode — the
        // parent's mode (Async) governs the Hangfire enqueue; children never re-enqueue.
        if (registration.Provider is ICompositeReportProvider composite)
        {
            var childKeys = await composite.GetChildReportKeysAsync(entityId, cancellationToken);
            if (childKeys.Count == 0)
                throw new NotFoundException(nameof(reportTypeKey), reportTypeKey);

            var childPdfs = new List<byte[]>(childKeys.Count);
            foreach (var childKey in childKeys)
                childPdfs.Add(await GenerateAsync(childKey, entityId, cancellationToken));

            var merged = await assembler.MergeAsync(childPdfs, cancellationToken);

            logger.LogInformation(
                "Composite report {ReportTypeKey} generated from [{ChildKeys}]: {Bytes} bytes",
                reportTypeKey, string.Join(", ", childKeys), merged.Length);

            return merged;
        }

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
