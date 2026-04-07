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
        decimal TotalLandAreaInSqWa,
        DateTime? AppointmentDate,
        decimal TotalBuildingCost);

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

        // Rental schedule from contract entries
        var contractSchedule = groupProperties
            .Where(p => p.RentalInfo is not null)
            .SelectMany(p => p.RentalInfo!.ScheduleEntries)
            .OrderBy(se => se.Year)
            .Select(se => new RentalScheduleRow(se.Year, se.ContractStart, se.ContractEnd, se.TotalAmount))
            .ToList();

        // Total land area
        var totalLandArea = groupProperties
            .Where(p => p.LandDetail is not null)
            .Sum(p => p.LandDetail!.TotalLandAreaInSqWa);

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
            @"SELECT ISNULL(SUM(bdd.PriceAfterDepreciation), 0)
              FROM appraisal.BuildingDepreciationDetails bdd
              INNER JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
              INNER JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
              INNER JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
              WHERE pgi.PropertyGroupId = @PropertyGroupId",
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
        var fraction = Math.Round(((decimal)days360 + 1) / 360m, 1);
        var firstMonths = Math.Round(((decimal)days360 + 1) / 30m, 1);

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
