namespace Appraisal.Application.Features.Project.DeleteProjectModel;

/// <summary>Handler for deleting a project model.</summary>
public class DeleteProjectModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<DeleteProjectModelCommand>
{
    public async Task<Unit> Handle(
        DeleteProjectModelCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        project.RemoveModel(command.ModelId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
