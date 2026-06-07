using Reporting.Application.Models;
using Reporting.Application.Providers.Sections;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles a composite <see cref="AppraisalSummaryModel"/> for FSD §2.1.7
/// "เล่มรายงานประเมินใน – Block" (Internal Appraisal Report – Block).
///
/// Composition:
///   1. Appraisal Summary – Block (via AppraisalSummaryBlockDataProvider.BuildAsync)
///   2. Comparison section (ComparisonSectionLoader)
///   3. WQS section        (WqsSectionLoader)
///   4. Sale Grid section  (SaleGridSectionLoader)
///   5. Appendix section   (AppendixSectionLoader) + PDF slot
///
/// Note: Land / Building / Construction sections are NOT included in the block variant
/// (§2.1.7 scope). The template guards on null so missing sections render nothing.
/// </summary>
public sealed class InternalBlockReportProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<InternalBlockReportProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "internal-report-block";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        // ── 1. Summary base (all block-summary fields) ───────────────────────────
        var model = await AppraisalSummaryBlockDataProvider.BuildAsync(
            connection, appraisalId, cancellationToken);

        // ── 2. Section loaders (same open connection, read-only) ─────────────────
        model.ComparisonSection = await ComparisonSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.WqsSection        = await WqsSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.SaleGridSection   = await SaleGridSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);

        var (appendixSection, pdfIds) = await AppendixSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.AppendixSection = appendixSection;

        if (pdfIds.Count > 0)
        {
            model.AttachmentsBySlot = new Dictionary<string, IReadOnlyList<Guid>>
            {
                ["appendix"] = pdfIds
            };
        }

        logger.LogDebug(
            "InternalBlock report assembled for appraisal {AppraisalId}: " +
            "hasComparison={HasComparison}, hasWqs={HasWqs}, hasSaleGrid={HasSaleGrid}, " +
            "hasAppendix={HasAppendix}, pdfAttachments={PdfCount}",
            appraisalId,
            model.ComparisonSection is not null,
            model.WqsSection is not null,
            model.SaleGridSection is not null,
            model.AppendixSection is not null,
            pdfIds.Count);

        return model;
    }
}
