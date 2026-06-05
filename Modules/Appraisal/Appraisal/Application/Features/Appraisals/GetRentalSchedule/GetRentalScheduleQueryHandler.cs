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

        // RentalInfo is present on lease-agreement properties and on plain land
        // (L/LB) flagged "rented out to others" — both are valid sources.
        var info = property.RentalInfo
            ?? throw new InvalidOperationException($"Rental info not found for property {query.PropertyId}");

        var computed = Shared.RentalScheduleComputer.Compute(info);
        var rows = computed.Select(c => new RentalScheduleRow(
            c.Year, c.ContractStart, c.ContractEnd,
            c.UpFront, c.ContractRentalFee, c.TotalAmount,
            c.ContractRentalFeeGrowthRatePercent)).ToList();
        return new GetRentalScheduleResult(rows);
    }
}
