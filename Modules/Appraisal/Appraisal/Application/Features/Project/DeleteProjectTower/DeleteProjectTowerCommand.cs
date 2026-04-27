namespace Appraisal.Application.Features.Project.DeleteProjectTower;

/// <summary>Command to delete a project tower (Condo only).</summary>
public record DeleteProjectTowerCommand(
    Guid AppraisalId,
    Guid TowerId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
