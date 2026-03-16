using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.CreateDocumentRequirement;

public class CreateDocumentRequirementCommandHandler : ICommandHandler<CreateDocumentRequirementCommand, CreateDocumentRequirementResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;

    public CreateDocumentRequirementCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateDocumentRequirementResult> Handle(
        CreateDocumentRequirementCommand command,
        CancellationToken cancellationToken)
    {
        var documentType = await _repository.GetDocumentTypeByIdAsync(command.DocumentTypeId, cancellationToken);
        if (documentType is null)
        {
            throw new InvalidOperationException($"Document type with ID {command.DocumentTypeId} not found");
        }

        var exists = await _repository.RequirementExistsAsync(
            command.DocumentTypeId,
            command.PropertyTypeCode,
            command.PurposeCode,
            cancellationToken);

        if (exists)
        {
            var typeDesc = command.PropertyTypeCode ?? "Application Level";
            var purposeDesc = command.PurposeCode is not null ? $" with purpose '{command.PurposeCode}'" : "";
            throw new InvalidOperationException(
                $"A requirement for document type '{documentType.Code}' already exists for {typeDesc}{purposeDesc}");
        }

        var requirement = (command.PropertyTypeCode, command.PurposeCode) switch
        {
            (null, null) => DocumentRequirement.CreateApplicationLevel(
                command.DocumentTypeId, command.IsRequired, command.Notes),

            (null, not null) => DocumentRequirement.CreateForPurpose(
                command.DocumentTypeId, command.PurposeCode, command.IsRequired, command.Notes),

            (not null, null) => DocumentRequirement.CreateForPropertyType(
                command.DocumentTypeId, command.PropertyTypeCode, command.IsRequired, command.Notes),

            (not null, not null) => DocumentRequirement.CreateForPropertyTypeAndPurpose(
                command.DocumentTypeId, command.PropertyTypeCode, command.PurposeCode, command.IsRequired, command.Notes)
        };

        _repository.AddRequirement(requirement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDocumentRequirementResult(requirement.Id);
    }
}
