using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.DeleteCondoTower;

public record DeleteCondoTowerCommand(
    Guid AppraisalId,
    Guid TowerId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
