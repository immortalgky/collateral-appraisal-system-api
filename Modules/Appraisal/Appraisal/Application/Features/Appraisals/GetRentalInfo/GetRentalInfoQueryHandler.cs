namespace Appraisal.Application.Features.Appraisals.GetRentalInfo;

public class GetRentalInfoQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetRentalInfoQuery, GetRentalInfoResult>
{
    public async Task<GetRentalInfoResult> Handle(
        GetRentalInfoQuery query,
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

        return new GetRentalInfoResult(
            DetailId: info.Id,
            AppraisalPropertyId: info.AppraisalPropertyId,
            NumberOfYears: info.NumberOfYears,
            FirstYearStartDate: info.FirstYearStartDate,
            ContractRentalFeePerYear: info.ContractRentalFeePerYear,
            UpFrontTotalAmount: info.UpFrontTotalAmount,
            GrowthRateType: info.GrowthRateType,
            GrowthRatePercent: info.GrowthRatePercent,
            GrowthIntervalYears: info.GrowthIntervalYears,
            UpFrontEntries: info.UpFrontEntries.OrderBy(e => e.AtYear).Select(e =>
                new UpFrontEntryDto(e.Id, e.AtYear, e.UpFrontAmount)).ToList(),
            GrowthPeriodEntries: info.GrowthPeriodEntries.OrderBy(e => e.FromYear).Select(e =>
                new GrowthPeriodEntryDto(e.Id, e.FromYear, e.ToYear, e.GrowthRate, e.GrowthAmount, e.TotalAmount)).ToList(),
            ScheduleEntries: info.ScheduleEntries.OrderBy(e => e.Year).Select(e =>
                new ScheduleEntryDto(e.Year, e.ContractStart, e.ContractEnd, e.UpFront, e.ContractRentalFee, e.TotalAmount, e.ContractRentalFeeGrowthRatePercent)).ToList(),
            ScheduleOverrides: info.ScheduleOverrides.Select(e =>
                new ScheduleOverrideDto(e.Year, e.UpFront, e.ContractRentalFee)).ToList());
    }
}
