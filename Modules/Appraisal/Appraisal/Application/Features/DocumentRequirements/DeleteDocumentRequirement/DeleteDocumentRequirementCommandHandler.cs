namespace Appraisal.Application.Features.DocumentRequirements.DeleteDocumentRequirement;

/// <summary>
/// Handler for DeleteDocumentRequirementCommand
/// Performs a soft delete by deactivating the requirement
/// </summary>
public class DeleteDocumentRequirementCommandHandler : ICommandHandler<DeleteDocumentRequirementCommand, DeleteDocumentRequirementResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public DeleteDocumentRequirementCommandHandler(
        IDocumentRequirementRepository repository,
        IAppraisalUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteDocumentRequirementResult> Handle(
        DeleteDocumentRequirementCommand command,
        CancellationToken cancellationToken)
    {
        var requirement = await _repository.GetRequirementByIdAsync(command.Id, cancellationToken);
        if (requirement is null)
        {
            throw new InvalidOperationException($"Document requirement with ID {command.Id} not found");
        }

        // Soft delete by deactivating
        requirement.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteDocumentRequirementResult(true);
    }
}
