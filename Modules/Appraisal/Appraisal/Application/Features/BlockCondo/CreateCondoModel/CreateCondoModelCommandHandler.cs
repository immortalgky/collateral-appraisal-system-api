namespace Appraisal.Application.Features.BlockCondo.CreateCondoModel;

/// <summary>
/// Handler for creating a condo model
/// </summary>
public class CreateCondoModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateCondoModelCommand, CreateCondoModelResult>
{
    public async Task<CreateCondoModelResult> Handle(
        CreateCondoModelCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate with block condo data
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Add model via aggregate
        var model = appraisal.AddCondoModel();

        // 3. Update model fields
        model.Update(
            command.ModelName,
            command.ModelDescription,
            command.BuildingNumber,
            command.StartingPriceMin,
            command.StartingPriceMax,
            command.HasMezzanine,
            command.UsableAreaMin,
            command.UsableAreaMax,
            command.StandardUsableArea,
            command.FireInsuranceCondition,
            command.RoomLayoutType,
            command.RoomLayoutTypeOther,
            command.GroundFloorMaterialType,
            command.GroundFloorMaterialTypeOther,
            command.UpperFloorMaterialType,
            command.UpperFloorMaterialTypeOther,
            command.BathroomFloorMaterialType,
            command.BathroomFloorMaterialTypeOther,
            command.ImageDocumentIds,
            command.Remark);

        // 4. Add area details
        if (command.AreaDetails is { Count: > 0 })
        {
            foreach (var dto in command.AreaDetails)
            {
                var areaDetail = CondoModelAreaDetail.Create(dto.AreaDescription, dto.AreaSize);
                model.AddAreaDetail(areaDetail);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateCondoModelResult(model.Id);
    }
}
