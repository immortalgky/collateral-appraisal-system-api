using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.DeleteCondoTower;

public class DeleteCondoTowerCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<DeleteCondoTowerCommand>
{
    public async Task<Unit> Handle(
        DeleteCondoTowerCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        appraisal.RemoveCondoTower(command.TowerId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
