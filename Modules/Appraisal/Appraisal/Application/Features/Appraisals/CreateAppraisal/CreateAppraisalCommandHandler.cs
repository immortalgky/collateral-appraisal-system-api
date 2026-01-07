namespace Appraisal.Application.Features.Appraisals.CreateAppraisal;

/// <summary>
/// Handler for creating a new Appraisal
/// </summary>
public class CreateAppraisalCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateAppraisalCommand, CreateAppraisalResult>
{
    public async Task<CreateAppraisalResult> Handle(
        CreateAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = Domain.Appraisals.Appraisal.Create(
            command.RequestId,
            command.AppraisalType,
            command.Priority,
            command.SLADays);

        await appraisalRepository.AddAsync(appraisal, cancellationToken);

        return new CreateAppraisalResult(appraisal.Id);
    }
}