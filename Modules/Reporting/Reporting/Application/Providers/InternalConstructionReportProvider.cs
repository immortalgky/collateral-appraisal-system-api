using Reporting.Application.Models;
using Reporting.Application.Providers.Sections;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles a composite <see cref="AppraisalSummaryModel"/> for FSD §2.1.6
/// "เล่มรายงานประเมินใน – ตรวจงวดงาน" (Internal Appraisal Report – Construction).
///
/// Composition:
///   1. Appraisal Summary – Construction (via AppraisalSummaryConstructionDataProvider.BuildAsync)
///   2. Land section         (LandSectionLoader)
///   3. Building section     (BuildingSectionLoader)
///   4. Construction section (ConstructionSectionLoader)
///   5. Comparison section   (ComparisonSectionLoader)
///   6. WQS section          (WqsSectionLoader)
///   7. Sale Grid section    (SaleGridSectionLoader)
///   8. Appendix section     (AppendixSectionLoader) + PDF slot
/// </summary>
public sealed class InternalConstructionReportProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<InternalConstructionReportProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "internal-report-construction";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        // ── 1. Summary base (all construction-summary fields) ────────────────────
        var model = await AppraisalSummaryConstructionDataProvider.BuildAsync(
            connection, appraisalId, cancellationToken);

        // ── 2. Section loaders (all read-only, same open connection) ────────────
        model.LandSection         = await LandSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.BuildingSection     = await BuildingSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.ConstructionSection = await ConstructionSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.ComparisonSection   = await ComparisonSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.WqsSection          = await WqsSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.SaleGridSection     = await SaleGridSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);

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
            "InternalConstruction report assembled for appraisal {AppraisalId}: " +
            "hasLand={HasLand}, hasBuilding={HasBuilding}, hasConstruction={HasConstruction}, " +
            "hasComparison={HasComparison}, hasWqs={HasWqs}, hasSaleGrid={HasSaleGrid}, " +
            "hasAppendix={HasAppendix}, pdfAttachments={PdfCount}",
            appraisalId,
            model.LandSection is not null,
            model.BuildingSection is not null,
            model.ConstructionSection is not null,
            model.ComparisonSection is not null,
            model.WqsSection is not null,
            model.SaleGridSection is not null,
            model.AppendixSection is not null,
            pdfIds.Count);

        return model;
    }
}
