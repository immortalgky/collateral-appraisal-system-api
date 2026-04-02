namespace Appraisal.Application.Features.Appraisals.GetRentalSchedule;

public class GetRentalScheduleQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetRentalScheduleQuery, GetRentalScheduleResult>
{
    public async Task<GetRentalScheduleResult> Handle(
        GetRentalScheduleQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            query.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var property = appraisal.GetProperty(query.PropertyId)
            ?? throw new PropertyNotFoundException(query.PropertyId);

        if (!property.PropertyType.IsLeaseAgreement)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a lease agreement property");

        var info = property.RentalInfo
            ?? throw new InvalidOperationException($"Rental info not found for property {query.PropertyId}");

        var rows = ComputeSchedule(info);
        return new GetRentalScheduleResult(rows);
    }

    private static List<RentalScheduleRow> ComputeSchedule(RentalInfo info)
    {
        var rows = new List<RentalScheduleRow>();
        if (info.NumberOfYears <= 0 || info.FirstYearStartDate is null)
            return rows;

        var startDate = info.FirstYearStartDate.Value;
        var currentFee = info.ContractRentalFeePerYear;

        for (var year = 1; year <= info.NumberOfYears; year++)
        {
            var contractStart = startDate.AddYears(year - 1);
            var contractEnd = startDate.AddYears(year).AddDays(-1);

            // Find up-front for this year
            var upFront = info.UpFrontEntries
                .Where(e => e.AtYear == year)
                .Sum(e => e.UpFrontAmount);

            // Calculate growth rate for this year
            decimal growthRatePercent = 0;

            if (year > 1)
            {
                if (info.GrowthRateType == "Period")
                {
                    // Apply growth every N years
                    if (info.GrowthIntervalYears > 0 && (year - 1) % info.GrowthIntervalYears == 0)
                    {
                        growthRatePercent = info.GrowthRatePercent;
                        currentFee += currentFee * growthRatePercent / 100m;
                    }
                }
                else if (info.GrowthRateType == "Property")
                {
                    // Use per-period growth entries
                    var periodEntry = info.GrowthPeriodEntries
                        .FirstOrDefault(e => year >= e.FromYear && year <= e.ToYear);

                    if (periodEntry is not null)
                    {
                        growthRatePercent = periodEntry.GrowthRate;
                        currentFee = periodEntry.TotalAmount;
                    }
                }
            }

            var totalAmount = currentFee + upFront;

            rows.Add(new RentalScheduleRow(
                Year: year,
                ContractStart: contractStart,
                ContractEnd: contractEnd,
                UpFront: upFront,
                ContractRentalFee: currentFee,
                TotalAmount: totalAmount,
                ContractRentalFeeGrowthRatePercent: growthRatePercent));
        }

        return rows;
    }
}
