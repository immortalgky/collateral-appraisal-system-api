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

        // Code reads these by code and carries them unconditionally (summary report D036 and the
        // D042/D043 originals it is re-typed from), so "don't use by default" is not a valid setting.
        if (command.CarryForwardByDefault == false && documentType.IsCarryForwardLocked)
            throw new BadRequestException($"Document type '{documentType.Code}' is always carried forward from the previous appraisal");

        documentType.Update(
            command.Name,
            command.Description,
            command.Category,
            command.SortOrder,
            command.NameTh,
            command.CarryForwardByDefault);

        if (command.IsActive && !documentType.IsActive)
            documentType.Activate();
        else if (!command.IsActive && documentType.IsActive)
            documentType.Deactivate();

        _repository.UpdateDocumentType(documentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateDocumentTypeResult(true);
    }
}
