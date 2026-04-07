namespace Appraisal.Application.Features.BlockCondo.DeleteCondoModel;

/// <summary>
/// Handler for deleting a condo model
/// </summary>
public class DeleteCondoModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<DeleteCondoModelCommand>
{
    public async Task<Unit> Handle(
        DeleteCondoModelCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate with block condo data
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Remove model via aggregate (throws if not found)
        appraisal.RemoveCondoModel(command.ModelId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
