using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.Appraisals.UpdateInspectionNumber;

public class UpdateInspectionNumberCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateInspectionNumberCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateInspectionNumberCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        if (appraisal.AppraisalType != AppraisalTypes.Progressive)
            throw new InvalidAppraisalStateException(
                "Only a construction-inspection (Progressive) appraisal has an inspection number.");

        appraisal.SetInspectionNumber(command.InspectionNumber);

        return MediatR.Unit.Value;
    }
}
