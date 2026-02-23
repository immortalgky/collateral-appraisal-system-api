namespace Appraisal.Application.Features.DocumentRequirements.UpdateDocumentRequirement;

/// <summary>
/// Handler for UpdateDocumentRequirementCommand
/// </summary>
public class UpdateDocumentRequirementCommandHandler : ICommandHandler<UpdateDocumentRequirementCommand, UpdateDocumentRequirementResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public UpdateDocumentRequirementCommandHandler(
        IDocumentRequirementRepository repository,
        IAppraisalUnitOfWork unitOfWork)
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

        // Update the requirement details
        requirement.Update(command.IsRequired, command.Notes);

        // Handle active status
        if (command.IsActive)
        {
            requirement.Activate();
        }
        else
        {
            requirement.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateDocumentRequirementResult(true);
    }
}
