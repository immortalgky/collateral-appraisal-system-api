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

        // Check for duplicate (same document type + collateral type combination)
        var exists = await _repository.RequirementExistsAsync(
            command.DocumentTypeId,
            command.CollateralTypeCode,
            cancellationToken);

        if (exists)
        {
            var typeDesc = command.CollateralTypeCode ?? "Application Level";
            throw new InvalidOperationException(
                $"A requirement for document type '{documentType.Code}' already exists for {typeDesc}");
        }

        // Create the requirement
        DocumentRequirement requirement;
        if (command.CollateralTypeCode is null)
        {
            requirement = DocumentRequirement.CreateApplicationLevel(
                command.DocumentTypeId,
                command.IsRequired,
                command.Notes);
        }
        else
        {
            requirement = DocumentRequirement.CreateForCollateral(
                command.DocumentTypeId,
                command.CollateralTypeCode,
                command.IsRequired,
                command.Notes);
        }

        _repository.AddRequirement(requirement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDocumentRequirementResult(requirement.Id);
    }
}
