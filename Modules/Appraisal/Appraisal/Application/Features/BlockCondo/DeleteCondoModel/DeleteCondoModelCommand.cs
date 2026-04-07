namespace Appraisal.Application.Features.BlockCondo.DeleteCondoModel;

/// <summary>
/// Command to delete a condo model from an appraisal
/// </summary>
public record DeleteCondoModelCommand(
    Guid AppraisalId,
    Guid ModelId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
