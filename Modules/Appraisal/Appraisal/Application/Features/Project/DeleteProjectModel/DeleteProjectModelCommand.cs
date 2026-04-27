namespace Appraisal.Application.Features.Project.DeleteProjectModel;

/// <summary>Command to delete a project model.</summary>
public record DeleteProjectModelCommand(
    Guid AppraisalId,
    Guid ModelId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
