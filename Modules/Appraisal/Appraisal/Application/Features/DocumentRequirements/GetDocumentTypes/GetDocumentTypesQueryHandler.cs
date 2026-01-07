namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentTypes;

/// <summary>
/// Handler for GetDocumentTypesQuery
/// </summary>
public class GetDocumentTypesQueryHandler : IQueryHandler<GetDocumentTypesQuery, GetDocumentTypesResult>
{
    private readonly IDocumentRequirementRepository _repository;

    public GetDocumentTypesQueryHandler(IDocumentRequirementRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDocumentTypesResult> Handle(
        GetDocumentTypesQuery query,
        CancellationToken cancellationToken)
    {
        var documentTypes = await _repository.GetAllDocumentTypesAsync(cancellationToken);

        var dtos = documentTypes.Select(dt => new DocumentTypeDto
        {
            Id = dt.Id,
            Code = dt.Code,
            Name = dt.Name,
            Description = dt.Description,
            Category = dt.Category,
            IsActive = dt.IsActive,
            SortOrder = dt.SortOrder,
            CreatedOn = dt.CreatedOn,
            UpdatedOn = dt.UpdatedOn
        }).ToList();

        return new GetDocumentTypesResult(dtos);
    }
}
