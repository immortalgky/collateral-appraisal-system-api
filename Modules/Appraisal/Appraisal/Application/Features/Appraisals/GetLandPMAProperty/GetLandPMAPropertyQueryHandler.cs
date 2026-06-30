using Appraisal.Application.Features.Appraisals.CreateLandProperty;
public class GetLandPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetLandPMAPropertyQuery, GetLandPMAPropertyResult>
{
    public async Task<GetLandPMAPropertyResult> Handle(
        GetLandPMAPropertyQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(query.PropertyId)
                    ?? throw new PropertyNotFoundException(query.PropertyId);

        // 3. Validate property type
        if (property.PropertyType != PropertyType.Land && property.PropertyType != PropertyType.LeaseAgreementLand)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a land property");

        var landDetail = property.LandDetail;

        // 4. Map to result
        return new GetLandPMAPropertyResult
        {
            PropertyId = property.Id,
            AppraisalId = property.AppraisalId,

            SellingPrice = property.SellingPrice,
            ForcedSalePrice = property.ForcedSalePrice,
            BuildingInsurancePrice = property.BuildingInsurancePrice,

            SubDistrict = landDetail?.Address?.SubDistrict,
            District = landDetail?.Address?.District,
            Province = landDetail?.Address?.Province,

            Titles = landDetail?.Titles.Select(title => new LandTitleItemData(
                title.Id,
                title.TitleNumber,
                title.TitleType,
                title.BookNumber,
                title.PageNumber,
                title.LandParcelNumber,
                title.SurveyNumber,
                title.MapSheetNumber,
                title.Rawang,
                title.AerialMapName,
                title.AerialMapNumber,
                title.Area?.Rai,
                title.Area?.Ngan,
                title.Area?.SquareWa,
                title.BoundaryMarkerType,
                title.BoundaryMarkerRemark,
                title.DocumentValidationResultType,
                title.IsMissingFromSurvey,
                title.GovernmentPricePerSqWa,
                title.GovernmentPrice,
                title.Remark
            )).ToList()
        };
    }
}