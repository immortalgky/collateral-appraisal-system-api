namespace Appraisal.Application.Features.Appraisals.UpdateCondoProperty;

/// <summary>
/// Handler for updating a condo pma property detail
/// </summary>
public class UpdateCondoPMAPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateCondoPMAPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateCondoPMAPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        // 3. Validate a property type
        if (property.PropertyType != PropertyType.Condo)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a condo property");

        var detail = property.CondoDetail
            ?? throw new InvalidOperationException($"Condo detail not found for property {command.PropertyId}");

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null)
        {
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province
            );
        }
        property.UpdatePrice(
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            buildingInsurancePrice: command.BuildingInsurancePrice
        );

        detail.Update(
            condoName: command.CondoName,
            ownerName: "",
            buildingNumber: command.BuildingNumber,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            condoRegistrationNumber: command.CondoRegistrationNumber,
            roomNumber: command.RoomNumber,
            floorNumber: command.FloorNumber,
            address: address
        );


        // 8. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
