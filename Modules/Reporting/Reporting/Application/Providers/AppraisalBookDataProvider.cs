using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Application.Services;
using Shared.Configuration;

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
    ISystemConfigurationReader configReader,
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
            var forceSaleRateDefault = await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", 70m, cancellationToken);

            switch (AppraisalBodyTypeClassifier.Classify(route.ProjectExists, route.AppraisalType))
            {
                case AppraisalBodyType.Block:
                    model = await AppraisalSummaryBlockDataProvider.BuildAsync(connection, appraisalId, forceSaleRateDefault, cancellationToken);
                    bodyType = "block";
                    break;
                case AppraisalBodyType.Construction:
                    model = await AppraisalSummaryConstructionDataProvider.BuildAsync(connection, appraisalId, forceSaleRateDefault, cancellationToken);
                    bodyType = "construction";
                    break;
                default:
                    model = await AppraisalSummaryLandBuildingDataProvider.BuildAsync(connection, appraisalId, forceSaleRateDefault, cancellationToken);
                    bodyType = "standard";
                    break;
            }
        }

        model.IsExternal = isExternal;
        model.BodyType = bodyType;

        // ── Load the shared detail sections ONCE (same open connection) ──────────────
        // Land + Building details: load one section per property, then assemble into
        // group-major buckets (กลุ่มที่ N → its land(s) → its building(s)).
        var landSections     = await LandSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        var buildingSections = await BuildingSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.PropertyGroups = BuildPropertyGroups(landSections, buildingSections);
        model.CondoSection        = await CondoSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.ConstructionSection = await ConstructionSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.MachineSection      = await MachineSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.ComparisonSections  = await ComparisonSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.WqsSections         = await WqsSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.SaleGridSections    = await SaleGridSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.CostMachineSections = await CostMachineSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);

        // ── Part B — new pricing-method sections ───────────────────────────────────
        model.IncomeSections     = await IncomeSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.ProfitRentSections = await ProfitRentSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.LeaseholdSections  = await LeaseholdSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.HypothesisSections = await HypothesisSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);

        // Each appendix group carries its own SLOT name; PDFs are keyed per group so they
        // merge under their own section heading rather than at the end of the document.
        var (appendixSection, appendixPdfSlots) = await AppendixSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.AppendixSection = appendixSection;
        if (appendixPdfSlots.Count > 0)
            model.AttachmentsBySlot = appendixPdfSlots;

        logger.LogDebug(
            "AppraisalBook model assembled for appraisal {AppraisalId}: isExternal={IsExternal}, bodyType={BodyType}",
            appraisalId, isExternal, bodyType ?? "external");

        return model;
    }

    /// <summary>
    /// Buckets per-property land and building sections into group-major
    /// <see cref="PropertyGroupDetail"/>s, ordered by group number. Groups carrying
    /// neither land nor building are omitted; ungrouped properties fall under group 0.
    /// </summary>
    private static IReadOnlyList<PropertyGroupDetail> BuildPropertyGroups(
        IReadOnlyList<LandSection> lands,
        IReadOnlyList<BuildingSection> buildings)
    {
        var groupNumbers = lands.Select(l => l.GroupNumber)
            .Concat(buildings.Select(b => b.GroupNumber))
            .Distinct()
            .OrderBy(n => n);

        return groupNumbers
            .Select(n =>
            {
                var groupLands = lands.Where(l => l.GroupNumber == n).ToList();
                var groupBuildings = buildings.Where(b => b.GroupNumber == n).ToList();
                var name = groupLands.Select(l => l.GroupName)
                    .Concat(groupBuildings.Select(b => b.GroupName))
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

                return new PropertyGroupDetail
                {
                    GroupNumber = n,
                    GroupName = name,
                    Lands = groupLands,
                    Buildings = groupBuildings
                };
            })
            .ToList();
    }

    private sealed class RouteRow
    {
        public string? AssignmentType { get; init; }
        public bool ProjectExists { get; init; }
        public string? AppraisalType { get; init; }
    }
}
