using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.DeleteDocumentType;

public class DeleteDocumentTypeCommandHandler : ICommandHandler<DeleteDocumentTypeCommand, DeleteDocumentTypeResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;

    public DeleteDocumentTypeCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteDocumentTypeResult> Handle(
        DeleteDocumentTypeCommand command,
        CancellationToken cancellationToken)
    {
        var documentType = await _repository.GetDocumentTypeByIdAsync(command.Id, cancellationToken);
        if (documentType is null)
        {
            throw new NotFoundException($"Document type with ID '{command.Id}' not found");
        }

        documentType.Deactivate();

        _repository.UpdateDocumentType(documentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteDocumentTypeResult(true);
    }
}
