namespace Appraisal.Application.Features.BlockCondo.GetCondoModels;

/// <summary>
/// Handler for getting all condo models
/// </summary>
public class GetCondoModelsQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetCondoModelsQuery, GetCondoModelsResult>
{
    public async Task<GetCondoModelsResult> Handle(
        GetCondoModelsQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate with block condo data
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // 2. Map models to DTOs
        var models = appraisal.CondoModels.Select(m => new CondoModelDto(
            Id: m.Id,
            AppraisalId: m.AppraisalId,
            ModelName: m.ModelName,
            ModelDescription: m.ModelDescription,
            BuildingNumber: m.BuildingNumber,
            StartingPriceMin: m.StartingPriceMin,
            StartingPriceMax: m.StartingPriceMax,
            HasMezzanine: m.HasMezzanine,
            UsableAreaMin: m.UsableAreaMin,
            UsableAreaMax: m.UsableAreaMax,
            StandardUsableArea: m.StandardUsableArea,
            FireInsuranceCondition: m.FireInsuranceCondition,
            RoomLayoutType: m.RoomLayoutType,
            RoomLayoutTypeOther: m.RoomLayoutTypeOther,
            GroundFloorMaterialType: m.GroundFloorMaterialType,
            GroundFloorMaterialTypeOther: m.GroundFloorMaterialTypeOther,
            UpperFloorMaterialType: m.UpperFloorMaterialType,
            UpperFloorMaterialTypeOther: m.UpperFloorMaterialTypeOther,
            BathroomFloorMaterialType: m.BathroomFloorMaterialType,
            BathroomFloorMaterialTypeOther: m.BathroomFloorMaterialTypeOther,
            ImageDocumentIds: m.ImageDocumentIds,
            AreaDetails: m.AreaDetails
                .Select(a => new CondoModelAreaDetailDto(a.Id, a.AreaDescription, a.AreaSize))
                .ToList(),
            Remark: m.Remark
        )).ToList();

        return new GetCondoModelsResult(models);
    }
}
