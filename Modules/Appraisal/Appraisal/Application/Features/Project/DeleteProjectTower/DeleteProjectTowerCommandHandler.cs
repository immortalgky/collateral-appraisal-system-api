namespace Appraisal.Application.Features.Project.DeleteProjectTower;

/// <summary>Handler for deleting a project tower.</summary>
public class DeleteProjectTowerCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<DeleteProjectTowerCommand>
{
    public async Task<Unit> Handle(
        DeleteProjectTowerCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        // Domain guard: RemoveTower throws if ProjectType != Condo
        project.RemoveTower(command.TowerId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
