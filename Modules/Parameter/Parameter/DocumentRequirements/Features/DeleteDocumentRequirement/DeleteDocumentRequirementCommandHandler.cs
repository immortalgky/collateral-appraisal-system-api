using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.DeleteDocumentRequirement;

public class DeleteDocumentRequirementCommandHandler : ICommandHandler<DeleteDocumentRequirementCommand, DeleteDocumentRequirementResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;

    public DeleteDocumentRequirementCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork)
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

        requirement.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteDocumentRequirementResult(true);
    }
}
