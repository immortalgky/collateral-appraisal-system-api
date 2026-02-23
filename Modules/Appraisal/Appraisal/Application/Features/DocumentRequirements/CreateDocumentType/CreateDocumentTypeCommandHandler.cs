namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentType;

/// <summary>
/// Handler for CreateDocumentTypeCommand
/// </summary>
public class CreateDocumentTypeCommandHandler : ICommandHandler<CreateDocumentTypeCommand, CreateDocumentTypeResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public CreateDocumentTypeCommandHandler(
        IDocumentRequirementRepository repository,
        IAppraisalUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateDocumentTypeResult> Handle(
        CreateDocumentTypeCommand command,
        CancellationToken cancellationToken)
    {
        // Check if code already exists
        var existing = await _repository.GetDocumentTypeByCodeAsync(command.Code, cancellationToken);
        if (existing is not null)
        {
            throw new BadRequestException($"Document type with code '{command.Code}' already exists");
        }

        // Create the document type
        var documentType = DocumentType.Create(
            command.Code,
            command.Name,
            command.Description,
            command.Category,
            command.SortOrder);

        _repository.AddDocumentType(documentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDocumentTypeResult(documentType.Id, documentType.Code, documentType.Name);
    }
}
