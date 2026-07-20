namespace Appraisal.Application.Features.Appraisals.CreateCondoPMAProperty;

/// <summary>
/// Handler for creating a condo pma property detail
/// </summary>
public class CreateCondoPMAPropertyCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateCondoPMAPropertyCommand, CreateCondoPMAPropertyResult>
{
    public async Task<CreateCondoPMAPropertyResult> Handle(
        CreateCondoPMAPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.AddCondoProperty();

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

        property.CondoDetail!.UpdatePmaFields(
            condoName: command.CondoName,
            ownerName: "",
            buildingNumber: command.BuildingNumber,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            condoRegistrationNumber: command.CondoRegistrationNumber,
            roomNumber: command.RoomNumber,
            floorNumber: command.FloorNumber,
            address: address
        );

        await unitOfWork.SaveChangesAsync(cancellationToken);
        
        if (command.GroupId.HasValue) appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);


        return new CreateCondoPMAPropertyResult(property.Id, property.CondoDetail.Id);
    }
}
