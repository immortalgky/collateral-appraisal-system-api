using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.UpdateDocumentType;

public class UpdateDocumentTypeCommandHandler : ICommandHandler<UpdateDocumentTypeCommand, UpdateDocumentTypeResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;

    public UpdateDocumentTypeCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateDocumentTypeResult> Handle(
        UpdateDocumentTypeCommand command,
        CancellationToken cancellationToken)
    {
        var documentType = await _repository.GetDocumentTypeByIdAsync(command.Id, cancellationToken);
        if (documentType is null)
        {
            throw new NotFoundException($"Document type with ID '{command.Id}' not found");
        }

        documentType.Update(
            command.Name,
            command.Description,
            command.Category,
            command.SortOrder);

        if (command.IsActive && !documentType.IsActive)
            documentType.Activate();
        else if (!command.IsActive && documentType.IsActive)
            documentType.Deactivate();

        _repository.UpdateDocumentType(documentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateDocumentTypeResult(true);
    }
}
