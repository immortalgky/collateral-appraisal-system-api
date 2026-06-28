using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;
using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.SetScopeRequirements;

/// <summary>
/// Reconciles the full set of document requirements for one (collateral type, purpose)
/// scope: included items are created or (re)activated with their required flag; existing
/// rows no longer included are deactivated. Atomic — one SaveChanges.
/// </summary>
public class SetScopeRequirementsCommandHandler
    : ICommandHandler<SetScopeRequirementsCommand, SetScopeRequirementsResult>
{
    private readonly IDocumentRequirementRepository _repository;
    private readonly IParameterUnitOfWork _unitOfWork;
    private readonly IParameterLookupService _parameterLookup;

    public SetScopeRequirementsCommandHandler(
        IDocumentRequirementRepository repository,
        IParameterUnitOfWork unitOfWork,
        IParameterLookupService parameterLookup)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _parameterLookup = parameterLookup;
    }

    public async Task<SetScopeRequirementsResult> Handle(
        SetScopeRequirementsCommand command,
        CancellationToken cancellationToken)
    {
        var propertyTypeCode = string.IsNullOrWhiteSpace(command.PropertyTypeCode)
            ? null
            : command.PropertyTypeCode;
        var purposeCode = string.IsNullOrWhiteSpace(command.PurposeCode) ? null : command.PurposeCode;

        if (propertyTypeCode is not null)
        {
            var valid = await _parameterLookup.GetValidCodesAsync(
                new ParameterDto(null, "CollateralType", null, null, null, null, true, null), cancellationToken);
            if (!valid.Contains(propertyTypeCode))
                throw new BadRequestException($"Invalid collateral type code '{propertyTypeCode}'");
        }

        if (purposeCode is not null)
        {
            var valid = await _parameterLookup.GetValidCodesAsync(
                new ParameterDto(null, "AppraisalPurpose", null, null, null, null, true, null), cancellationToken);
            if (!valid.Contains(purposeCode))
                throw new BadRequestException($"Invalid purpose code '{purposeCode}'");
        }

        var existing = await _repository.GetRequirementsByScopeAsync(
            propertyTypeCode, purposeCode, cancellationToken);
        var existingByDoc = existing.ToDictionary(r => r.DocumentTypeId);
        var includedIds = new HashSet<Guid>();

        foreach (var item in command.Items)
        {
            includedIds.Add(item.DocumentTypeId);

            if (existingByDoc.TryGetValue(item.DocumentTypeId, out var current))
            {
                current.Update(item.IsRequired, current.Notes);
                current.Activate();
                _repository.UpdateRequirement(current);
            }
            else
            {
                var documentType = await _repository.GetDocumentTypeByIdAsync(item.DocumentTypeId, cancellationToken);
                if (documentType is null)
                    throw new BadRequestException($"Document type with ID {item.DocumentTypeId} not found");

                var requirement = (propertyTypeCode, purposeCode) switch
                {
                    (null, null) => DocumentRequirement.CreateApplicationLevel(
                        item.DocumentTypeId, item.IsRequired),
                    (null, not null) => DocumentRequirement.CreateForPurpose(
                        item.DocumentTypeId, purposeCode, item.IsRequired),
                    (not null, null) => DocumentRequirement.CreateForPropertyType(
                        item.DocumentTypeId, propertyTypeCode, item.IsRequired),
                    (not null, not null) => DocumentRequirement.CreateForPropertyTypeAndPurpose(
                        item.DocumentTypeId, propertyTypeCode, purposeCode, item.IsRequired),
                };
                _repository.AddRequirement(requirement);
            }
        }

        // Deactivate rows that are no longer included in this scope.
        foreach (var current in existing)
        {
            if (!includedIds.Contains(current.DocumentTypeId) && current.IsActive)
            {
                current.Deactivate();
                _repository.UpdateRequirement(current);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetScopeRequirementsResult(true);
    }
}
