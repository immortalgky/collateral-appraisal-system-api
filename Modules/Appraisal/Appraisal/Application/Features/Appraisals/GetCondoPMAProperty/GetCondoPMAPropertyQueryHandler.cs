public class GetCondoPMAPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetCondoPMAPropertyQuery, GetCondoPMAPropertyResult>
{
    public async Task<GetCondoPMAPropertyResult> Handle(
        GetCondoPMAPropertyQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(query.PropertyId)
                    ?? throw new PropertyNotFoundException(query.PropertyId);

        // 3. Validate a property type
        if (property.PropertyType != PropertyType.Condo)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a condo property");

        // 4. Get the condo detail
        var detail = property.CondoDetail
                    ?? throw new InvalidOperationException($"Condo detail not found for property {query.PropertyId}");

        // 5. Map to result
        return new GetCondoPMAPropertyResult(
            PropertyId: property.Id,
            AppraisalId: property.AppraisalId,
            BuildingNumber: detail.BuildingNumber,
            BuiltOnTitleNumber: detail.BuiltOnTitleNumber,
            CondoRegistrationNumber: detail.CondoRegistrationNumber,
            CondoName: detail.CondoName,
            RoomNumber: detail.RoomNumber,
            FloorNumber: detail.FloorNumber,
            SubDistrict: detail.Address?.SubDistrict,
            District: detail.Address?.District,
            Province: detail.Address?.Province,
            BuildingInsurancePrice: detail.BuildingInsurancePrice,
            SellingPrice: detail.SellingPrice,
            ForceSellingPrice: detail.ForcedSalePrice);
    }
}