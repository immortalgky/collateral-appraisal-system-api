namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentRequirement;

/// <summary>
/// Handler for CreateDocumentRequirementCommand
/// </summary>
public class CreateDocumentRequirementCommandHandler : ICommandHandler<CreateDocumentRequirementCommand, CreateDocumentRequirementResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public CreateDocumentRequirementCommandHandler(
        IDocumentRequirementRepository repository,
        IAppraisalUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateDocumentRequirementResult> Handle(
        CreateDocumentRequirementCommand command,
        CancellationToken cancellationToken)
    {
        // Verify document type exists
        var documentType = await _repository.GetDocumentTypeByIdAsync(command.DocumentTypeId, cancellationToken);
        if (documentType is null)
        {
            throw new InvalidOperationException($"Document type with ID {command.DocumentTypeId} not found");
        }

        // Check for duplicate (same document type + property type + purpose combination)
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

        // Create the requirement based on which fields are provided
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
