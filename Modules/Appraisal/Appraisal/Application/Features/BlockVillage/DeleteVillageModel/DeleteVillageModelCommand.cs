namespace Appraisal.Application.Features.BlockVillage.DeleteVillageModel;

public record DeleteVillageModelCommand(Guid AppraisalId, Guid ModelId)
    : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
