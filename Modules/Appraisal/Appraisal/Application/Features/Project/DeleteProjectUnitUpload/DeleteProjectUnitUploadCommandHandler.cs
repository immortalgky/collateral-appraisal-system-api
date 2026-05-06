namespace Appraisal.Application.Features.Project.DeleteProjectUnitUpload;

/// <summary>Handler for deleting a project unit upload and its units.</summary>
public class DeleteProjectUnitUploadCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<DeleteProjectUnitUploadCommand>
{
    public async Task<Unit> Handle(
        DeleteProjectUnitUploadCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        project.RemoveUnitUpload(command.UploadId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
