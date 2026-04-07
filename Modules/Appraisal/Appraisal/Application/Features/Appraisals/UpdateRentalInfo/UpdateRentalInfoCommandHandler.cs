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

        // Compute schedule server-side from rental info fields, apply overrides
        Appraisal.Application.Features.Appraisals.Shared.RentalScheduleComputer.ComputeAndSave(info, command.ScheduleOverrides);

        return MediatR.Unit.Value;
    }
}
