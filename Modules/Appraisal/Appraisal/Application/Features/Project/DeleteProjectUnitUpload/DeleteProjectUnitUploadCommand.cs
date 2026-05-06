namespace Appraisal.Application.Features.Project.DeleteProjectUnitUpload;

/// <summary>Command to delete a unit upload batch and its associated units.</summary>
public record DeleteProjectUnitUploadCommand(
    Guid AppraisalId,
    Guid UploadId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
