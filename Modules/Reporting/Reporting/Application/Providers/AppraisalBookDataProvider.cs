using Reporting.Application.Models;
using Reporting.Application.Providers.Sections;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Unified "Appraisal Book" report (เล่มรายงานการประเมิน) — replaces the former three reports
/// external-appraisal-report (§2.1.2), internal-report-construction (§2.1.6) and
/// internal-report-block (§2.1.7).
///
/// One template (<c>appraisal-book.html</c>) branches on two axes resolved from the appraisal data:
///   • internal vs external — from the latest non-cancelled
///     <c>appraisal.AppraisalAssignments.AssignmentType</c> ('Internal'/'External').
///       external → cover page + company letter;
///       internal → a new internal cover page + a summary body.
///   • body type (internal only) — block (an <c>appraisal.Projects</c> row exists),
///     construction (<c>Appraisals.AppraisalType == 'Progressive'</c>) or standard (otherwise).
///
/// The header/body is built by reusing the existing builders; the shared detail sections
/// (Land/Building/Condo/Construction/Machine/Comparison/WQS/SaleGrid/CostMachine/Appendix) are
/// loaded ONCE here for every variant and guarded by null in the template.
/// </summary>
public sealed class AppraisalBookDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalBookDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-book";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        // ── Resolve the two routing axes in one round-trip ───────────────────────────
        const string routeSql = """
            SELECT
                (SELECT TOP 1 aa.AssignmentType
                 FROM appraisal.AppraisalAssignments aa
                 WHERE aa.AppraisalId = @AppraisalId
                   AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                 ORDER BY aa.AssignedAt DESC, aa.Id DESC)                          AS AssignmentType,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.Projects pr
                                       WHERE pr.AppraisalId = @AppraisalId)
                          THEN 1 ELSE 0 END AS bit)                                AS ProjectExists,
                (SELECT a.AppraisalType FROM appraisal.Appraisals a
                 WHERE a.Id = @AppraisalId AND a.IsDeleted = 0)                    AS AppraisalType;
            """;

        var routeParams = new DynamicParameters();
        routeParams.Add("AppraisalId", appraisalId);

        var route = await connection.QuerySingleOrDefaultAsync<RouteRow>(
            new CommandDefinition(routeSql, routeParams, cancellationToken: cancellationToken));

        if (route is null)
            throw new NotFoundException("Appraisal", entityId);

        bool isExternal = string.Equals(route.AssignmentType, "External", StringComparison.OrdinalIgnoreCase);

        // ── Build the header/body model ──────────────────────────────────────────────
        // Internal body variant uses the shared classifier so appraisal-book and appraisal-summary
        // can never dispatch the same appraisal differently.
        AppraisalSummaryModel model;
        string? bodyType = null;

        if (isExternal)
        {
            model = await ExternalBookBuilder.BuildAsync(connection, appraisalId, cancellationToken);
        }
        else
        {
            switch (AppraisalBodyTypeClassifier.Classify(route.ProjectExists, route.AppraisalType))
            {
                case AppraisalBodyType.Block:
                    model = await AppraisalSummaryBlockDataProvider.BuildAsync(connection, appraisalId, cancellationToken);
                    bodyType = "block";
                    break;
                case AppraisalBodyType.Construction:
                    model = await AppraisalSummaryConstructionDataProvider.BuildAsync(connection, appraisalId, cancellationToken);
                    bodyType = "construction";
                    break;
                default:
                    model = await AppraisalSummaryLandBuildingDataProvider.BuildAsync(connection, appraisalId, cancellationToken);
                    bodyType = "standard";
                    break;
            }
        }

        model.IsExternal = isExternal;
        model.BodyType = bodyType;

        // ── Load the shared detail sections ONCE (same open connection) ──────────────
        model.LandSection         = await LandSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.BuildingSection     = await BuildingSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.CondoSection        = await CondoSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.ConstructionSection = await ConstructionSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.MachineSection      = await MachineSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.ComparisonSection   = await ComparisonSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.WqsSection          = await WqsSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.SaleGridSection     = await SaleGridSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.CostMachineSection  = await CostMachineSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);

        var (appendixSection, appendixPdfIds) = await AppendixSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.AppendixSection = appendixSection;
        if (appendixPdfIds.Count > 0)
        {
            model.AttachmentsBySlot = new Dictionary<string, IReadOnlyList<Guid>>
            {
                ["appendix"] = appendixPdfIds
            };
        }

        logger.LogDebug(
            "AppraisalBook model assembled for appraisal {AppraisalId}: isExternal={IsExternal}, bodyType={BodyType}",
            appraisalId, isExternal, bodyType ?? "external");

        return model;
    }

    private sealed class RouteRow
    {
        public string? AssignmentType { get; init; }
        public bool ProjectExists { get; init; }
        public string? AppraisalType { get; init; }
    }
}
