namespace Appraisal.Application.Features.DocumentRequirements.UpdateDocumentType;

/// <summary>
/// Handler for UpdateDocumentTypeCommand
/// </summary>
public class UpdateDocumentTypeCommandHandler : ICommandHandler<UpdateDocumentTypeCommand, UpdateDocumentTypeResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public UpdateDocumentTypeCommandHandler(
        IDocumentRequirementRepository repository,
        IAppraisalUnitOfWork unitOfWork)
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

        // Update the document type
        documentType.Update(
            command.Name,
            command.Description,
            command.Category,
            command.SortOrder);

        // Handle active status
        if (command.IsActive && !documentType.IsActive)
        {
            documentType.Activate();
        }
        else if (!command.IsActive && documentType.IsActive)
        {
            documentType.Deactivate();
        }

        _repository.UpdateDocumentType(documentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateDocumentTypeResult(true);
    }
}
