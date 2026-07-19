using System.Text.RegularExpressions;

namespace Reporting.Application.Services;

/// <summary>
/// Orchestrates the full report generation pipeline:
/// Registry → DataProvider → TemplateStore → TemplateRenderer → PdfAssembler → bytes.
/// </summary>
public sealed class ReportGenerationService(
    IReportRegistry registry,
    IReportEntityResolver entityResolver,
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

        // Callers pass a human-friendly number (AppraisalNumber, or MeetingNo for Meeting reports);
        // resolve it to the entity Guid the providers expect. Values already in Guid form — a direct
        // id, or the recursive composite child calls below — pass through unchanged.
        var resolvedEntityId = await entityResolver.ResolveAsync(
            entityId, registration.Category, cancellationToken);

        logger.LogInformation(
            "Generating report {ReportTypeKey} for entity {EntityId}",
            reportTypeKey, resolvedEntityId);

        // [0] Composite reports (e.g. unified "appraisal-summary") resolve to a set of child
        // report keys whose PDFs are rendered through this same pipeline and concatenated.
        // The children are NOT seeded in ReportDefinitions; they resolve via ReportRegistry's
        // provider fallback (registered IReportDataProvider, no DB row → IsEnabled=true,
        // TemplateId==key), so the recursive call's IsEnabled check passes. Each child is
        // rendered inline/synchronously here regardless of its resolved GenerationMode — the
        // parent's mode (Async) governs the Hangfire enqueue; children never re-enqueue.
        if (registration.Provider is ICompositeReportProvider composite)
        {
            var childKeys = await composite.GetChildReportKeysAsync(resolvedEntityId, cancellationToken);
            if (childKeys.Count == 0)
                throw new NotFoundException(nameof(reportTypeKey), reportTypeKey);

            var childPdfs = new List<byte[]>(childKeys.Count);
            foreach (var childKey in childKeys)
                childPdfs.Add(await GenerateAsync(childKey, resolvedEntityId, cancellationToken));

            var merged = await assembler.MergeAsync(childPdfs, cancellationToken);

            logger.LogInformation(
                "Composite report {ReportTypeKey} generated from [{ChildKeys}]: {Bytes} bytes",
                reportTypeKey, string.Join(", ", childKeys), merged.Length);

            return merged;
        }

        // [1] Resolve data
        var model = await registration.Provider.GetModelAsync(resolvedEntityId, cancellationToken);

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

    /// <summary>
    /// Generates a browser-renderable HTML string for the given report (used by the in-browser
    /// paged.js preview). Reuses steps [0]–[3] of <see cref="GenerateAsync"/> (registry lookup,
    /// entity resolution, data load, template load, Scriban render) but skips PDF assembly
    /// (<see cref="IPdfAssembler.AssembleAsync"/>) — there is no PDF to produce here.
    ///
    /// The raw rendered HTML is then post-processed so it is self-contained and viewable
    /// directly by a browser, since it never passes through headless Chromium (which is what
    /// resolves file:// image sources and local font/logo file references in the PDF path):
    ///   - Appendix/photo <c>&lt;img src="file://…"&gt;</c> sources (built by
    ///     <c>AppendixSectionLoader.ToFileUri</c>) are rewritten to the anonymous, browser-
    ///     reachable <c>GET /documents/{id}/download</c> route.
    ///   - The Sarabun fonts and bank logo, referenced as local files in the template partials,
    ///     are inlined as base64 data URIs so the HTML has no other local file dependencies.
    ///   - Composite reports (multiple templates merged into one PDF) have no single rendered
    ///     HTML to preview and are not supported by this path.
    /// </summary>
    public async Task<string> GenerateHtmlAsync(
        string reportTypeKey,
        string entityId,
        CancellationToken cancellationToken)
    {
        var registration = registry.TryGet(reportTypeKey)
            ?? throw new NotFoundException(nameof(reportTypeKey), reportTypeKey);

        // Same kill-switch honoured by GenerateAsync.
        if (!registration.IsEnabled)
            throw new NotFoundException(nameof(reportTypeKey), reportTypeKey);

        // Composite reports render multiple child templates into separate PDFs and merge the
        // bytes — there's no single HTML document to preview.
        if (registration.Provider is ICompositeReportProvider)
            throw new NotFoundException(nameof(reportTypeKey), reportTypeKey);

        var resolvedEntityId = await entityResolver.ResolveAsync(
            entityId, registration.Category, cancellationToken);

        logger.LogInformation(
            "Generating HTML preview for report {ReportTypeKey}, entity {EntityId}",
            reportTypeKey, resolvedEntityId);

        // [1] Resolve data
        var model = await registration.Provider.GetModelAsync(resolvedEntityId, cancellationToken);

        // [2] Load template
        var templateHtml = await templateStore.GetTemplateAsync(
            registration.TemplateId, cancellationToken);

        // [3] Render HTML (no slot splitting — the browser preview has no attachment merge step)
        var html = await renderer.RenderRawAsync(templateHtml, model, cancellationToken);

        html = RewriteAppendixImageSources(html);
        html = InlineFontsAndLogo(html);

        return html;
    }

    // ── HTML post-processing for the browser-preview path ──────────────────────────────────

    // Matches src="file://..." / src='file:///...' (2 or 3 slashes; AppendixSectionLoader's
    // Uri.AbsoluteUri emits 3). ReDoS-hardened with a 1-second match timeout (S6444).
    private static readonly Regex FileImageSrcRegex = new(
        """src\s*=\s*(?<q>['"])file:/{2,3}(?<path>[^'"]*)\k<q>""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Rewrites every appendix/photo <c>&lt;img src="file://…/{documentId}{ext}"&gt;</c> to the
    /// anonymous <c>/documents/{documentId}/download</c> route so a browser (which cannot read
    /// file:// URIs sourced from the API's local disk) can render it. The GUID is recovered from
    /// the last path segment's filename (upload files are stored as <c>{documentId}{ext}</c> —
    /// see AppendixSectionLoader). Non-file:// sources, and file:// sources whose filename isn't
    /// a parseable GUID, are left untouched.
    /// </summary>
    private static string RewriteAppendixImageSources(string html) =>
        FileImageSrcRegex.Replace(html, match =>
        {
            var quote = match.Groups["q"].Value;
            var rawPath = match.Groups["path"].Value;

            string decodedPath;
            try
            {
                decodedPath = Uri.UnescapeDataString(rawPath);
            }
            catch (UriFormatException)
            {
                return match.Value; // unparseable escape sequence — leave untouched
            }

            var fileName = decodedPath.Replace('\\', '/');
            var lastSlash = fileName.LastIndexOf('/');
            if (lastSlash >= 0)
                fileName = fileName[(lastSlash + 1)..];

            var lastDot = fileName.LastIndexOf('.');
            var nameWithoutExtension = lastDot >= 0 ? fileName[..lastDot] : fileName;

            return Guid.TryParse(nameWithoutExtension, out var documentId)
                ? $"src={quote}/documents/{documentId}/download{quote}"
                : match.Value;
        });

    // Fonts/logo are local files under AppContext.BaseDirectory/Templates (Content-copied by
    // Reporting.csproj) that the PDF path resolves via headless Chromium's file:// access, but a
    // browser previewing the raw HTML has no such access — inline them as base64 data URIs
    // instead. Cached in static fields: the files never change at runtime, so each is read from
    // disk at most once per process.
    private static readonly Regex SarabunRegularUrlRegex = new(
        """url\(\s*(['"]?)Sarabun\.ttf\1\s*\)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex SarabunBoldUrlRegex = new(
        """url\(\s*(['"]?)Sarabun-Bold\.ttf\1\s*\)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex LogoSrcRegex = new(
        """src\s*=\s*(['"])logo-lh-bank\.svg\1""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly object FontCacheLock = new();
    private static string? _sarabunRegularBase64;
    private static string? _sarabunBoldBase64;
    private static string? _logoBase64;

    private static string InlineFontsAndLogo(string html)
    {
        html = SarabunRegularUrlRegex.Replace(
            html, $"url('data:font/ttf;base64,{GetCachedBase64(ref _sarabunRegularBase64, "Sarabun.ttf")}')");
        html = SarabunBoldUrlRegex.Replace(
            html, $"url('data:font/ttf;base64,{GetCachedBase64(ref _sarabunBoldBase64, "Sarabun-Bold.ttf")}')");
        html = LogoSrcRegex.Replace(
            html, $"src=\"data:image/svg+xml;base64,{GetCachedBase64(ref _logoBase64, "logo-lh-bank.svg")}\"");
        return html;
    }

    private static string GetCachedBase64(ref string? cache, string fileName)
    {
        if (cache is not null)
            return cache;

        lock (FontCacheLock)
        {
            if (cache is not null)
                return cache;

            var path = Path.Combine(AppContext.BaseDirectory, "Templates", fileName);
            var bytes = File.Exists(path) ? File.ReadAllBytes(path) : [];
            cache = Convert.ToBase64String(bytes);
            return cache;
        }
    }
}
