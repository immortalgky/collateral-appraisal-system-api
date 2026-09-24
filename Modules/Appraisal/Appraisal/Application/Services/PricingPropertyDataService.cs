using Appraisal.Domain.Appraisals;
using Dapper;
using Shared.Data;

namespace Appraisal.Application.Services;

/// <summary>
/// Shared service for fetching property group data and building appraisal schedules.
/// Used by both Leasehold and ProfitRent pricing analysis handlers.
/// </summary>
public class PricingPropertyDataService(
    IAppraisalRepository appraisalRepository,
    ISqlConnectionFactory sqlConnectionFactory
)
{
    public record RentalScheduleRow(int Year, DateTime ContractStart, DateTime ContractEnd, decimal TotalAmount);

    public record AppraisalScheduleRow(decimal Year, decimal NumberOfMonths, decimal ContractRentalFee);

    public record PropertyGroupData(
        List<RentalScheduleRow> ContractSchedule,
        /// <summary>
        /// NET appraisable land area, despite the historical name: populated below from
        /// <c>LandAppraisalDetail.NetLandAreaInSqWa</c> — registered title area LESS the areas the
        /// appraiser listed as not appraisable. Trust this docstring, not the name.
        /// <para>
        /// The name was kept on purpose. The design note for the net/deed split called renaming a
        /// field in place "the bug most likely to pass review", so the split changed the meaning and
        /// left the name, which puts the whole burden on this comment. Every reader of this field is
        /// a pricing path and every one of them wants net. Anything stating the parcel as a legal
        /// fact — the report book, Collateral Master, the AS400 regulatory exports — deliberately
        /// reads <c>LandAppraisalDetail.TotalLandAreaInSqWa</c> off the domain instead and keeps the
        /// registered deed figure. Both are correct for their own readers.
        /// </para>
        /// </summary>
        decimal TotalLandAreaInSqWa,
        DateTime? AppointmentDate,
        decimal TotalBuildingCost);

    /// <summary>
    /// Returns the sum of <c>LandAppraisalDetail.NetLandAreaInSqWa</c> across all properties in a
    /// property group (i.e. C01) — registered title area LESS the areas the appraiser listed as not
    /// appraisable (encroachment, land used by others, public waterway, …).
    /// Returns null when the group is empty or not found.
    /// <para>
    /// This service is the ONE place that chooses net over gross. Every pricing path reads land area
    /// through here, so nothing downstream has to know the difference. Anything reporting the parcel
    /// as a legal fact — Collateral Master, the AS400 exports, the per-title rows of the book —
    /// reads <c>TotalLandAreaInSqWa</c> off the domain instead and keeps the registered figure.
    /// </para>
    /// </summary>
    public async Task<decimal?> GetTotalLandAreaFromTitlesAsync(
        Guid propertyGroupId, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.GetOpenConnection();

        var appraisalId = await connection.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT AppraisalId FROM appraisal.PropertyGroups WHERE Id = @PropertyGroupId",
            new { PropertyGroupId = propertyGroupId });

        if (appraisalId is null)
            return null;

        var propertyIds = (await connection.QueryAsync<Guid>(
            "SELECT AppraisalPropertyId FROM appraisal.PropertyGroupItems WHERE PropertyGroupId = @PropertyGroupId",
            new { PropertyGroupId = propertyGroupId })).ToHashSet();

        if (propertyIds.Count == 0)
            return null;

        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            appraisalId.Value, cancellationToken);

        if (appraisal is null)
            return null;

        var landProperties = appraisal.Properties
            .Where(p => propertyIds.Contains(p.Id) && p.LandDetail is not null)
            .ToList();

        if (landProperties.Count == 0)
            return null;

        return landProperties.Sum(p => p.LandDetail!.NetLandAreaInSqWa);
    }

    /// <summary>
    /// Returns the depreciated building total for a property group — the sum of
    /// <c>BuildingDepreciationDetails.PriceAfterDepreciation</c> across its properties, which is the
    /// same figure the appraisal-summary report totals as รวมมูลค่าสิ่งปลูกสร้าง.
    /// Returns 0 when the group has no building schedule.
    /// </summary>
    public async Task<decimal> GetTotalBuildingCostAsync(
        Guid propertyGroupId, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.GetOpenConnection();

        return await connection.QueryFirstOrDefaultAsync<decimal>(
            new CommandDefinition(
                BuildingCostSql,
                new { PropertyGroupId = propertyGroupId },
                cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Final Cost Value of every building in a property group, keyed by AppraisalPropertyId —
    /// the per-building figure <see cref="GetTotalBuildingCostAsync"/> sums. Empty when the group
    /// has no building. Independent of any Building Cost method: Hypothesis reads it directly.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetBuildingFinalCostValuesAsync(
        Guid propertyGroupId, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.GetOpenConnection();

        var rows = await connection.QueryAsync<(Guid AppraisalPropertyId, decimal FinalCostValue)>(
            new CommandDefinition(
                BuildingFinalCostValuesSql,
                new { PropertyGroupId = propertyGroupId },
                cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.AppraisalPropertyId, r => r.FinalCostValue);
    }

    /// <summary>
    /// Appraisable land area for a property group, in square wa — title area less the appraiser's
    /// listed deductions, the same figure <see cref="GetTotalLandAreaFromTitlesAsync"/> returns.
    /// </summary>
    /// <remarks>
    /// A SQL-only counterpart to <see cref="GetTotalLandAreaFromTitlesAsync"/> for read paths:
    /// that one loads the whole Appraisal aggregate with every property and detail just to sum a
    /// computed property, which is far too much for an endpoint on a screen's load path.
    /// KEEP IN SYNC with LandAppraisalDetail.NetLandAreaInSqWa — same three area terms, same
    /// per-property subtraction, same floor at zero.
    /// </remarks>
    public async Task<decimal?> GetTotalLandAreaInSqWaAsync(
        Guid propertyGroupId, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.GetOpenConnection();

        return await connection.QueryFirstOrDefaultAsync<decimal?>(
            new CommandDefinition(
                LandAreaSql,
                new { PropertyGroupId = propertyGroupId },
                cancellationToken: cancellationToken));
    }

    // Driven from LandAppraisalDetails, not from LandTitles: the deduction is recorded once per
    // PROPERTY while the area is spread over its title deeds, so subtracting inside the per-title
    // SUM would deduct it once per deed. Gross is totalled per property first, then the property's
    // own deduction comes off, then the properties are summed.
    //
    // SUM over zero rows still yields NULL, which callers read as "this group has no land" — the
    // same thing GetTotalLandAreaFromTitlesAsync returns for a group with no land properties. A
    // property whose deeds carry no area keeps contributing NULL exactly as it did before.
    private const string LandAreaSql =
        """
        SELECT SUM(
                   CASE WHEN d.GrossArea - ISNULL(lad.DeductedAreaInSqWa, 0) < 0
                        THEN 0
                        ELSE d.GrossArea - ISNULL(lad.DeductedAreaInSqWa, 0)
                   END)
        FROM appraisal.LandAppraisalDetails lad
        INNER JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = lad.AppraisalPropertyId
        CROSS APPLY (
            SELECT SUM(ISNULL(lt.AreaRai, 0) * 400
                     + ISNULL(lt.AreaNgan, 0) * 100
                     + ISNULL(lt.AreaSquareWa, 0)) AS GrossArea
            FROM appraisal.LandTitles lt
            WHERE lt.LandAppraisalDetailId = lad.Id
        ) d
        WHERE pgi.PropertyGroupId = @PropertyGroupId
        """;

    // Per building in the group: its Final Cost Value. Shared by the group total below and by
    // Hypothesis, which prices each house model at the value of the building it is mapped to.
    private const string BuildingFinalCostValuesSql =
        """
            -- One row per building: the appraiser's own final cost wins, otherwise the sum of the
            -- schedule (every row, Non-Building included — that is what the Cost approach prices),
            -- rounded to the nearest 1,000.
            --
            -- The rounding is not cosmetic: it is the Building Cost Value the appraiser sees on the
            -- property form, and both report providers already close their tables on the same figure.
            -- Pricing read the raw sum, so a group with no override priced a few hundred baht away
            -- from the number printed beside it. ROUND(x, -3) matches the C# side's
            -- Math.Round(x / 1000m, MidpointRounding.AwayFromZero) * 1000m.
            -- KEEP IN SYNC with BuildingSectionLoader's totalValueAfterDepr and
            -- AppraisalSummaryLandBuildingDataProvider's buildingValueById.
            -- ISNULL: a building with no schedule and no override is worth 0 (the group SUM already
            -- skipped it); without it the per-building read would hand Dapper a NULL decimal.
            SELECT bad.AppraisalPropertyId,
                   ISNULL(COALESCE(bad.FinalCostValueOverride, ROUND(SUM(bdd.PriceAfterDepreciation), -3)), 0) AS FinalCostValue
            FROM appraisal.BuildingAppraisalDetails bad
            INNER JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            INNER JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
            LEFT JOIN appraisal.BuildingDepreciationDetails bdd ON bdd.BuildingAppraisalDetailId = bad.Id
            WHERE pgi.PropertyGroupId = @PropertyGroupId
            GROUP BY bad.Id, bad.AppraisalPropertyId, bad.FinalCostValueOverride
        """;

    private const string BuildingCostSql =
        $"""
        SELECT ISNULL(SUM(x.FinalCostValue), 0)
        FROM (
        {BuildingFinalCostValuesSql}
        ) x
        """;

    /// <summary>
    /// Fetches rental schedule, land area, and appointment date for a property group.
    /// </summary>
    public async Task<PropertyGroupData> GetPropertyDataAsync(
        Guid propertyGroupId, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.GetOpenConnection();

        var appraisalId = await connection.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT AppraisalId FROM appraisal.PropertyGroups WHERE Id = @PropertyGroupId",
            new { PropertyGroupId = propertyGroupId });

        if (appraisalId is null)
            return new PropertyGroupData([], 0, null, 0);

        var propertyIds = (await connection.QueryAsync<Guid>(
            "SELECT AppraisalPropertyId FROM appraisal.PropertyGroupItems WHERE PropertyGroupId = @PropertyGroupId",
            new { PropertyGroupId = propertyGroupId })).ToHashSet();

        if (propertyIds.Count == 0)
            return new PropertyGroupData([], 0, null, 0);

        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            appraisalId.Value, cancellationToken);

        if (appraisal is null)
            return new PropertyGroupData([], 0, null, 0);

        var groupProperties = appraisal.Properties
            .Where(p => propertyIds.Contains(p.Id))
            .ToList();

        // Rental schedule from contract entries — sourced from any rental-bearing
        // property in the group. RentalInfo is attached only to lease-agreement
        // properties and to plain land (L/LB) flagged "rented out to others",
        // so a mixed group only needs at least one such property.
        var contractSchedule = groupProperties
            .Where(p => p.RentalInfo is not null)
            .SelectMany(p => p.RentalInfo!.ScheduleEntries)
            .OrderBy(se => se.Year)
            .Select(se => new RentalScheduleRow(se.Year, se.ContractStart, se.ContractEnd, se.TotalAmount))
            .ToList();

        // Appraisable land area — registered area less the appraiser's listed deductions.
        var totalLandArea = groupProperties
            .Where(p => p.LandDetail is not null)
            .Sum(p => p.LandDetail!.NetLandAreaInSqWa);

        // Appointment date
        var appointmentDate = await connection.QueryFirstOrDefaultAsync<DateTime?>(
            @"SELECT TOP 1 ap.AppointmentDateTime
              FROM appraisal.Appointments ap
              INNER JOIN appraisal.AppraisalAssignments aa ON ap.AssignmentId = aa.Id
              WHERE aa.AppraisalId = @AppraisalId AND ap.Status != 'Cancelled'
              ORDER BY ap.AppointmentDateTime DESC",
            new { AppraisalId = appraisalId });

        // Total building cost from depreciation details
        var totalBuildingCost = await connection.QueryFirstOrDefaultAsync<decimal>(
            BuildingCostSql,
            new { PropertyGroupId = propertyGroupId });

        return new PropertyGroupData(contractSchedule, totalLandArea, appointmentDate, totalBuildingCost);
    }

    /// <summary>
    /// Build appraisal schedule from contract schedule + appointment date.
    /// Re-indexes using DAYS360 (mirrors frontend computeAppraisalSchedule).
    /// </summary>
    public static List<AppraisalScheduleRow> BuildAppraisalSchedule(
        List<RentalScheduleRow> contractRows,
        DateTime? appointmentDate)
    {
        if (contractRows.Count == 0)
            return [];

        if (appointmentDate is null)
        {
            return contractRows.Select(r =>
                new AppraisalScheduleRow(r.Year, 12, r.TotalAmount)).ToList();
        }

        var appraisal = appointmentDate.Value;

        // Find which contract row the appraisal date falls into
        int startIdx = -1;
        for (int i = 0; i < contractRows.Count; i++)
        {
            if (appraisal >= contractRows[i].ContractStart && appraisal <= contractRows[i].ContractEnd)
            {
                startIdx = i;
                break;
            }
        }

        if (startIdx == -1)
        {
            if (appraisal < contractRows[0].ContractStart)
            {
                return contractRows.Select((r, i) =>
                    new AppraisalScheduleRow(i + 1, 12, r.TotalAmount)).ToList();
            }
            return [];
        }

        // Calculate fraction using DAYS360
        var firstRow = contractRows[startIdx];
        var days360 = Days360Between(appraisal, firstRow.ContractEnd);
        // AwayFromZero, not Math.Round's default. The screen computes the same figure as
        // `Math.round(((days360 + 1) / 360) * 10) / 10` (calculateLeasehold.ts), i.e. halves up, and
        // its own comment cites Excel's ROUND — which is also halves up. Banker's rounding disagreed
        // at ten reachable day counts per two-year window (days360 + 1 = 18, 90, 162, 234, 306, …),
        // every one of them landing exactly on x.x5 because 360 divides evenly: 0.25 became 0.2 here
        // and 0.3 on screen. That is 0.1 of a year of rent on the first period AND on the `year` that
        // drives the PV factors, so the whole discounting chain shifted. At days360 + 1 = 18 it was
        // worse than a wrong number: 0.05 rounded to 0.0, the `fraction > 0` guard below then
        // dropped the first schedule row outright while the screen still showed it.
        var fraction = Math.Round(((decimal)days360 + 1) / 360m, 1, MidpointRounding.AwayFromZero);
        // Same rule for the month count. It has no counterpart on the screen, so nothing disagreed
        // with it — but it is the same derivation from the same days, and leaving one of the pair on
        // banker's is how the next reader concludes the mode here is arbitrary.
        var firstMonths = Math.Round(((decimal)days360 + 1) / 30m, 1, MidpointRounding.AwayFromZero);

        var result = new List<AppraisalScheduleRow>();

        if (fraction > 0)
        {
            result.Add(new AppraisalScheduleRow(
                fraction, firstMonths, firstRow.TotalAmount * fraction));
        }

        for (int i = startIdx + 1; i < contractRows.Count; i++)
        {
            result.Add(new AppraisalScheduleRow(
                fraction + (i - startIdx), 12, contractRows[i].TotalAmount));
        }

        return result;
    }

    /// <summary>
    /// DAYS360 calculation (US/NASD method) — same as Excel's DAYS360.
    /// </summary>
    public static int Days360Between(DateTime start, DateTime end)
    {
        var d1 = Math.Min(start.Day, 30);
        var d2 = end.Day;
        if (d1 == 30) d2 = Math.Min(d2, 30);

        return (end.Year - start.Year) * 360 + (end.Month - start.Month) * 30 + (d2 - d1);
    }
}
