using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

/// <summary>
/// Handler for getting a land property by ID
/// </summary>
public class GetLandPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetLandPropertyQuery, GetLandPropertyResult>
{
    public async Task<GetLandPropertyResult> Handle(
        GetLandPropertyQuery query,
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
        if (property.PropertyType != PropertyType.Land)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a land property");

        var landDetail = property.LandDetail;

        // 4. Map to result
        return new GetLandPropertyResult
        {
            PropertyId = property.Id,
            AppraisalId = property.AppraisalId,
            SequenceNumber = property.SequenceNumber,
            PropertyType = property.PropertyType.ToString(),
            Description = property.Description,

            LandDetailId = landDetail?.Id,
            PropertyName = landDetail?.PropertyName,
            LandDescription = landDetail?.LandDescription,
            OwnerName = landDetail?.OwnerName,
            IsOwnerVerified = landDetail?.IsOwnerVerified ?? false,
            HasObligation = landDetail?.HasObligation ?? false,
            ObligationDetails = landDetail?.ObligationDetails,

            Street = landDetail?.Street,
            Soi = landDetail?.Soi,
            Village = landDetail?.Village,
            SubDistrict = landDetail?.Address?.SubDistrict,
            District = landDetail?.Address?.District,
            Province = landDetail?.Address?.Province,

            Latitude = landDetail?.Coordinates?.Latitude,
            Longitude = landDetail?.Coordinates?.Longitude,

            Remark = landDetail?.Remark
        };
    }
}
