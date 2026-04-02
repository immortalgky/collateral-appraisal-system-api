namespace Appraisal.Application.Features.Appraisals.UpdateRentalInfo;

public class UpdateRentalInfoCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateRentalInfoCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateRentalInfoCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        if (!property.PropertyType.IsLeaseAgreement)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a lease agreement property");

        var info = property.RentalInfo
            ?? throw new InvalidOperationException($"Rental info not found for property {command.PropertyId}");

        // Update scalar fields
        info.Update(
            numberOfYears: command.NumberOfYears,
            firstYearStartDate: command.FirstYearStartDate,
            contractRentalFeePerYear: command.ContractRentalFeePerYear,
            upFrontTotalAmount: command.UpFrontTotalAmount,
            growthRateType: command.GrowthRateType,
            growthRatePercent: command.GrowthRatePercent,
            growthIntervalYears: command.GrowthIntervalYears);

        // Replace up-front entries if provided
        if (command.UpFrontEntries is not null)
        {
            info.ClearUpFrontEntries();
            foreach (var entry in command.UpFrontEntries)
            {
                info.AddUpFrontEntry(entry.AtYear, entry.UpFrontAmount);
            }
        }

        // Replace growth period entries if provided
        if (command.GrowthPeriodEntries is not null)
        {
            info.ClearGrowthPeriodEntries();
            foreach (var entry in command.GrowthPeriodEntries)
            {
                info.AddGrowthPeriodEntry(entry.FromYear, entry.ToYear, entry.GrowthRate, entry.GrowthAmount, entry.TotalAmount);
            }
        }

        // Replace schedule entries if provided
        if (command.ScheduleEntries is not null)
        {
            info.ClearScheduleEntries();
            foreach (var entry in command.ScheduleEntries)
                info.AddScheduleEntry(entry.Year, entry.ContractStart, entry.ContractEnd,
                    entry.UpFront, entry.ContractRentalFee, entry.TotalAmount, entry.ContractRentalFeeGrowthRatePercent);
        }

        // Replace schedule overrides if provided
        if (command.ScheduleOverrides is not null)
        {
            info.ClearScheduleOverrides();
            foreach (var o in command.ScheduleOverrides)
                info.SetScheduleOverride(o.Year, o.UpFront, o.ContractRentalFee);
        }

        return MediatR.Unit.Value;
    }
}
