namespace Appraisal.Application.Features.DocumentRequirements.DeleteDocumentType;

/// <summary>
/// Handler for DeleteDocumentTypeCommand
/// Performs soft delete by deactivating the document type
/// </summary>
public class DeleteDocumentTypeCommandHandler : ICommandHandler<DeleteDocumentTypeCommand, DeleteDocumentTypeResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public DeleteDocumentTypeCommandHandler(
        IDocumentRequirementRepository repository,
        IAppraisalUnitOfWork unitOfWork)
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

        // Soft delete - deactivate the document type
        documentType.Deactivate();

        _repository.UpdateDocumentType(documentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteDocumentTypeResult(true);
    }
}
