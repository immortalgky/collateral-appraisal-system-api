namespace Appraisal.Application.Features.BlockVillage.DeleteVillageModel;

public class DeleteVillageModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<DeleteVillageModelCommand>
{
    public async Task<Unit> Handle(
        DeleteVillageModelCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        appraisal.RemoveVillageModel(command.ModelId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
