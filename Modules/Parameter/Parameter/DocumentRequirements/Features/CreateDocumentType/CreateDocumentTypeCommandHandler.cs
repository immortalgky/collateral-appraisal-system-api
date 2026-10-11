using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.CreateDocumentType;

public class CreateDocumentTypeCommandHandler : ICommandHandler<CreateDocumentTypeCommand, CreateDocumentTypeResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;

    public CreateDocumentTypeCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateDocumentTypeResult> Handle(
        CreateDocumentTypeCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetDocumentTypeByCodeAsync(command.Code, cancellationToken);
        if (existing is not null)
        {
            throw new BadRequestException($"Document type with code '{command.Code}' already exists");
        }

        var documentType = DocumentType.Create(
            command.Code,
            command.Name,
            command.Description,
            command.Category,
            command.SortOrder,
            command.NameTh,
            command.CarryForwardByDefault);

        // Same rule as update: these codes are always carried forward, so "don't use" is not valid.
        if (!command.CarryForwardByDefault && documentType.IsCarryForwardLocked)
            throw new BadRequestException($"Document type '{documentType.Code}' is always carried forward from the previous appraisal");

        _repository.AddDocumentType(documentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDocumentTypeResult(documentType.Id, documentType.Code, documentType.Name);
    }
}
