using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.ReorderDocumentTypes;

public class ReorderDocumentTypesCommandHandler
    : ICommandHandler<ReorderDocumentTypesCommand, ReorderDocumentTypesResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;

    public ReorderDocumentTypesCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReorderDocumentTypesResult> Handle(
        ReorderDocumentTypesCommand command,
        CancellationToken cancellationToken)
    {
        foreach (var item in command.Items)
        {
            var documentType = await _repository.GetDocumentTypeByIdAsync(item.Id, cancellationToken);
            if (documentType is null)
                continue;

            documentType.SetSortOrder(item.SortOrder);
            _repository.UpdateDocumentType(documentType);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReorderDocumentTypesResult(true);
    }
}
