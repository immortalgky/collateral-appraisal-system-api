using Reporting.Contracts;
using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Application.Services;
using Shared.Configuration;
using Shared.Time;

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
    IDateTimeProvider dateTimeProvider,
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
        const string routeSql =
            """
            SELECT
                (SELECT TOP 1 aa.AssignmentType
                 FROM appraisal.AppraisalAssignments aa
                 WHERE aa.AppraisalId = @AppraisalId
                   AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                 ORDER BY aa.AssignedAt DESC, aa.Id DESC)                          AS AssignmentType,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.Projects pr
                                       WHERE pr.AppraisalId = @AppraisalId)
                          THEN 1 ELSE 0 END AS bit)                                AS ProjectExists,
                a.AppraisalType                                                    AS AppraisalType,
            """ + AppraisalSummaryDataProvider.StandardFormFlagsSql + """

            -- Same load-bearing FROM as AppraisalSummaryDataProvider, and for the same two reasons.
            -- Without it this is a bare SELECT that returns a row for ANY id, so the `route is null`
            -- guard below could never fire and an unknown appraisal rendered a blank book with HTTP 200.
            -- It also replaces an `AND a.IsDeleted = 0` that used to sit on the AppraisalType subquery:
            -- that filter did not skip the appraisal, it only blanked its type, so a soft-deleted
            -- Progressive appraisal was silently reclassified Standard and rendered the wrong body.
            FROM appraisal.Appraisals a
            WHERE a.Id = @AppraisalId;
            """;

        var routeParams = new DynamicParameters();
        routeParams.Add("AppraisalId", appraisalId);

        var route = await connection.QuerySingleOrDefaultAsync<RouteRow>(
            new CommandDefinition(routeSql, routeParams, cancellationToken: cancellationToken));

        if (route is null)
            throw new NotFoundException("Appraisal", entityId);

        bool isExternal = string.Equals(route.AssignmentType, "External", StringComparison.OrdinalIgnoreCase);

        // ── Build the header/body model ──────────────────────────────────────────────
        // Which summary form(s) the appraisal has comes from the same rules appraisal-summary uses
        // (AppraisalBodyTypeClassifier for block / construction, AppraisalSummaryDataProvider.StandardFormKeys
        // for the land-building / condo / machine fan-out), so the two reports can never dispatch the same
        // appraisal differently.
        var forceSaleRateDefault = await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", 70m, cancellationToken);
        var forms = await BuildSummaryFormsAsync(connection, appraisalId, route, forceSaleRateDefault, firstOnly: isExternal, cancellationToken);

        AppraisalSummaryModel model;
        // Set for external books too (the template branches on is_external first): the detail pages still
        // need to know whether this is a block / construction / standard appraisal.
        string bodyType = forms[0].BodyType;

        if (isExternal)
        {
            // The company letter does not print a summary form, but its ที่ตั้งทรัพย์สิน / พรบ.ผังเมือง must be the
            // very values the bank's appraisal summary prints, so they are borrowed from the first form.
            model = await ExternalBookBuilder.BuildAsync(connection, appraisalId, forms[0].Model, forceSaleRateDefault, cancellationToken);
        }
        else
        {
            // Internal: the front page IS the summary form(s), pasted as appraisal-summary renders them.
            model = forms[0].Model;
            foreach (var extra in forms.Skip(1))
                extra.Model.BodyType = extra.BodyType;
            model.ExtraSummaryForms = forms.Skip(1).Select(f => f.Model).ToList();
        }

        model.IsExternal = isExternal;
        model.BodyType = bodyType;

        // The internal book prints the same วิธีการประเมิน / เหตุผลการเลือกวิธี the company letter does (section 5).
        if (!isExternal)
        {
            var pricing = await ExternalBookBuilder.LoadPricingAsync(connection, appraisalId, cancellationToken);
            model.PriceMethod = pricing.PriceMethod;
            model.ApproachValues = pricing.Rows;
            model.ApproachValuesMultiGroup = pricing.MultiGroup;
            model.SelectionReason = pricing.Reason;
        }

        // Section 5.1 วิธีการประเมินมูลค่า in prose, from the same approach rows both books carry (internal: loaded just above; external: ExternalBookBuilder).
        model.PriceMethodNarrative = PricingNarrative.Build(model.ApproachValues, model.ApproachValuesMultiGroup);

        // ── Load the shared detail sections ONCE (same open connection) ──────────────
        // Land + Building details: load one section per property, then assemble into
        // group-major buckets (กลุ่มที่ N → its land(s) → its building(s)).
        var landSections     = await LandSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        var buildingSections = await BuildingSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);
        model.PropertyGroups = BuildPropertyGroups(landSections, buildingSections);
        model.CondoSection        = await CondoSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.ConstructionSection = await ConstructionSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.MachineSection      = await MachineSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        // The block loader's 9-result batch only for an appraisal that has a project (the route query already knows): every other book skips it.
        model.BlockSection        = route.ProjectExists
            ? await BlockBookSectionLoader.LoadAsync(connection, appraisalId, dateTimeProvider.ToApplicationTime, cancellationToken)
            : null;
        // No land/condo property gave the internal cover a ที่ตั้งทรัพย์สิน (a machine-only appraisal,
        // or anchors with nothing to print yet) → the machine location. The external book
        // (ExternalBookBuilder) is left as it is.
        if (!isExternal && ThaiAddressFormatter.Stated(model.CollateralAddress) is null)
            model.CollateralAddress = ThaiAddressFormatter.Stated(model.MachineSection?.MachineLocation);
        model.CoverLines = CoverNarrative.Build(model);
        if (isExternal)
        {
            // The company letter's owner rows and อาคารสิ่งปลูกสร้าง row cover every deed / building, not the first (the condo owners come from the rooms ExternalBookBuilder loaded).
            model.LandOwner = LetterOwnerRows.LandOwners(model);
            model.BuildingOwner = LetterOwnerRows.BuildingOwners(model);
            model.CondoOwner = LetterOwnerRows.CondoOwners(model);
            model.BuildingDetailsText = LetterOwnerRows.BuildingDetails(model);
            // The letter's ประเภททรัพย์สิน row is the cover's first line (what it is); the letter has its own area and location rows. A body type the cover
            // does not describe keeps the property-type summary ExternalBookBuilder wrote.
            model.PropertyTypeSummary = CoverNarrative.LetterPropertyType(model);
            // The external book's สรุปผลการประเมินมูลค่าทรัพย์สิน page: the rows the letter used to carry, plus ที่ตั้ง, การรอนสิทธิ and the approach(es) used.
            model.LocationText = CoverNarrative.LocationLine(model);
            model.PropertyBlocks = ExternalSummaryRows.PropertyBlocks(model);
            model.EvictionText = LetterOwnerRows.LandEvictions(model);
            model.ApproachMethodsText = PricingNarrative.SelectedApproaches(model.ApproachValues);
            if (model.AllLands.Count > 0 || model.CondoObligationItems.Count > 0)
                (model.ObligationMortgage, model.ObligationOthers) = LetterOwnerRows.Obligations(model);
        }
        // Which property types each group holds: ComparisonSubject decides which groups are condo-only from them, and such a group reads its
        // own units' figures (CondoSection.GroupFigures), not the appraisal-wide condo totals.
        model.GroupPropertyTypes = await ComparisonSubject.LoadGroupPropertyTypesAsync(connection, appraisalId, cancellationToken);
        model.ComparisonSections  = await ComparisonSectionLoader.LoadAllAsync(
            connection, appraisalId, dateTimeProvider.ToApplicationTime, g => ComparisonSubject.Values(model, g), cancellationToken);
        model.WqsSections         = await WqsSectionLoader.LoadAllAsync(
            connection, appraisalId, cancellationToken, g => ComparisonSubject.Values(model, g));
        model.SaleGridSections    = await SaleGridSectionLoader.LoadAllAsync(
            connection, appraisalId, dateTimeProvider.ToApplicationTime, g => ComparisonSubject.Values(model, g), cancellationToken);
        model.CostMachineSections = await CostMachineSectionLoader.LoadAllAsync(
            connection, appraisalId, dateTimeProvider.ApplicationNow.Year + 543, dateTimeProvider.ToApplicationTime, cancellationToken);

        // ── Part B — new pricing-method sections ───────────────────────────────────
        model.IncomeSections     = await IncomeSectionLoader.LoadAllAsync(connection, appraisalId, dateTimeProvider.ToApplicationTime, cancellationToken);
        model.ProfitRentSections = await ProfitRentSectionLoader.LoadAllAsync(connection, appraisalId, dateTimeProvider.ToApplicationTime, cancellationToken);
        model.LeaseholdSections  = await LeaseholdSectionLoader.LoadAllAsync(connection, appraisalId, dateTimeProvider.ToApplicationTime, cancellationToken);
        model.HypothesisSections = await HypothesisSectionLoader.LoadAllAsync(connection, appraisalId, cancellationToken);

        // Each appendix group carries its own SLOT name; PDFs are keyed per group so they
        // merge under their own section heading rather than at the end of the document.
        var (appendixSection, appendixPdfSlots) = await AppendixSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        model.AppendixSection = appendixSection;
        model.LawRecords = await LawSectionLoader.LoadAsync(connection, appraisalId, cancellationToken);
        if (appendixPdfSlots.Count > 0)
            model.AttachmentsBySlot = appendixPdfSlots;

        WarnWhenGroupValuesDoNotTie(model, appraisalId, isExternal);

        logger.LogDebug(
            "AppraisalBook model assembled for appraisal {AppraisalId}: isExternal={IsExternal}, bodyType={BodyType}",
            appraisalId, isExternal, bodyType);

        return model;
    }

    /// <summary>
    /// The value the book prints in its conclusion must be the sum of the values of the approaches the groups used
    /// (section 6). When it is not, the book contradicts itself — log it with both figures so the data can be fixed.
    /// </summary>
    private void WarnWhenGroupValuesDoNotTie(AppraisalSummaryModel model, Guid appraisalId, bool isExternal)
    {
        var used = model.ApproachValues.Where(r => !r.IsReference).ToList();
        if (used.Count == 0 || used.Any(r => r.Value is null))
            return;

        var groupSum = used.Sum(r => r.Value!.Value);
        // An internal book whose appraisal fans out to several summary forms prints the sum of the forms (see book-conclusion).
        var printed = isExternal
            ? model.CollateralValue
            : model.TotalAppraisalValue is null ? null : model.TotalAppraisalValue + model.ExtraSummaryForms.Sum(f => f.TotalAppraisalValue ?? 0m);
        if (printed is null || Math.Abs(printed.Value - groupSum) <= 1m)
            return;

        logger.LogWarning(
            "AppraisalBook {AppraisalId}: the conclusion prints {Printed} but the group values used in the valuation add up to {GroupSum} ({Groups})",
            appraisalId, printed, groupSum,
            string.Join(", ", used.Select(r => $"group {r.GroupNumber} {r.ApproachType}={r.Value}")));
    }

    /// <summary>
    /// The summary form(s) the appraisal resolves to, in the order appraisal-summary renders them.
    /// Standard appraisals fan out to land-building, condo and machine forms by the property types they hold;
    /// one with none of them keeps the land-building form (what the book always did).
    /// Property remarks are left out of every embedded form — the book's own sections already print them.
    /// </summary>
    private static async Task<List<(string BodyType, AppraisalSummaryModel Model)>> BuildSummaryFormsAsync(
        System.Data.IDbConnection connection,
        Guid appraisalId,
        RouteRow route,
        decimal forceSaleRateDefault,
        bool firstOnly,
        CancellationToken ct)
    {
        var forms = new List<(string, AppraisalSummaryModel)>();

        switch (AppraisalBodyTypeClassifier.Classify(route.ProjectExists, route.AppraisalType))
        {
            case AppraisalBodyType.Block:
                forms.Add(("block", await AppraisalSummaryBlockDataProvider.BuildAsync(connection, appraisalId, forceSaleRateDefault, ct)));
                return forms;
            case AppraisalBodyType.Construction:
                forms.Add(("construction", await AppraisalSummaryConstructionDataProvider.BuildAsync(connection, appraisalId, forceSaleRateDefault, ct)));
                return forms;
        }

        var keys = AppraisalSummaryDataProvider.StandardFormKeys(route.HasLandBuilding, route.HasCondo, route.HasMachine);
        if (keys.Count == 0)
            keys.Add("appraisal-summary-land-building");
        if (firstOnly)
            keys = keys.Take(1).ToList();

        foreach (var key in keys)
        {
            switch (key)
            {
                case "appraisal-summary-condo":
                    forms.Add(("condo", await AppraisalSummaryCondoDataProvider.BuildAsync(
                        connection, appraisalId, forceSaleRateDefault, includePropertyRemarks: false, ct)));
                    break;
                case "appraisal-summary-machine":
                    forms.Add(("machine", await AppraisalSummaryMachineDataProvider.BuildAsync(
                        connection, appraisalId, forceSaleRateDefault, includePropertyRemarks: false, ct)));
                    break;
                default:
                    forms.Add(("standard", await AppraisalSummaryLandBuildingDataProvider.BuildAsync(
                        connection, appraisalId, forceSaleRateDefault, includePropertyRemarks: false, ct)));
                    break;
            }
        }

        return forms;
    }

    /// <summary>
    /// Buckets per-property land and building sections into group-major
    /// <see cref="PropertyGroupDetail"/>s, ordered by group number. Groups carrying
    /// neither land nor building are omitted; ungrouped properties fall under group 0.
    /// </summary>
    internal static IReadOnlyList<PropertyGroupDetail> BuildPropertyGroups(
        IReadOnlyList<LandSection> lands,
        IReadOnlyList<BuildingSection> buildings)
    {
        var groupNumbers = lands.Select(l => l.GroupNumber)
            .Concat(buildings.Select(b => b.GroupNumber))
            .Distinct()
            .OrderBy(n => n);

        var number = 0;
        return groupNumbers
            .Select(n =>
            {
                var groupLands = lands.Where(l => l.GroupNumber == n).ToList();
                var groupBuildings = buildings.Where(b => b.GroupNumber == n).ToList();
                var name = groupLands.Select(l => l.GroupName)
                    .Concat(groupBuildings.Select(b => b.GroupName))
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

                // The building pages and the cost table both say "รายการที่ N": same number, real buildings only.
                // A building the appraiser chose not to value (IsAppraisable == false) takes no number; the book lists it after the numbered ones.
                foreach (var b in groupBuildings.Where(b => !b.IsDevelopmentOnly && b.IsAppraisable != false))
                    b.BuildingNumber = ++number;

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
        public bool HasCondo { get; init; }
        public bool HasMachine { get; init; }
        public bool HasLandBuilding { get; init; }
    }
}
