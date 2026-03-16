using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.UpdateDocumentRequirement;

public class UpdateDocumentRequirementCommandHandler : ICommandHandler<UpdateDocumentRequirementCommand, UpdateDocumentRequirementResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;

    public UpdateDocumentRequirementCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateDocumentRequirementResult> Handle(
        UpdateDocumentRequirementCommand command,
        CancellationToken cancellationToken)
    {
        var requirement = await _repository.GetRequirementByIdAsync(command.Id, cancellationToken);
        if (requirement is null)
        {
            throw new InvalidOperationException($"Document requirement with ID {command.Id} not found");
        }

        requirement.Update(command.IsRequired, command.Notes);

        if (command.IsActive)
            requirement.Activate();
        else
            requirement.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateDocumentRequirementResult(true);
    }
}
