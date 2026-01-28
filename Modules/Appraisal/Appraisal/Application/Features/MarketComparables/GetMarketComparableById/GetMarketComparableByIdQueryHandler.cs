using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparableById;

/// <summary>
/// Handler for getting a market comparable by ID with full details
/// </summary>
public class GetMarketComparableByIdQueryHandler(
    IMarketComparableRepository marketComparableRepository
) : IQueryHandler<GetMarketComparableByIdQuery, GetMarketComparableByIdResult>
{
    public async Task<GetMarketComparableByIdResult> Handle(
        GetMarketComparableByIdQuery query,
        CancellationToken cancellationToken)
    {
        var comparable = await marketComparableRepository.GetByIdWithDetailsAsync(
            query.Id,
            cancellationToken);

        if (comparable is null)
        {
            throw new InvalidOperationException(
                $"Market comparable with ID {query.Id} not found");
        }

        var dto = new MarketComparableDetailDto
        {
            Id = comparable.Id,
            ComparableNumber = comparable.ComparableNumber,
            PropertyType = comparable.PropertyType,

            // Location
            Province = comparable.Province,
            District = comparable.District,
            SubDistrict = comparable.SubDistrict,
            Address = comparable.Address,
            Latitude = comparable.Latitude,
            Longitude = comparable.Longitude,

            // Transaction
            TransactionType = comparable.TransactionType,
            TransactionDate = comparable.TransactionDate,
            TransactionPrice = comparable.TransactionPrice,
            PricePerUnit = comparable.PricePerUnit,
            UnitType = comparable.UnitType,

            // Data Quality
            DataSource = comparable.DataSource,
            DataConfidence = comparable.DataConfidence,
            IsVerified = comparable.IsVerified,
            VerifiedAt = comparable.VerifiedAt,
            VerifiedBy = comparable.VerifiedBy,

            // Status
            Status = comparable.Status,
            ExpiryDate = comparable.ExpiryDate,

            // Survey
            SurveyDate = comparable.SurveyDate,
            SurveyedBy = comparable.SurveyedBy,

            // Notes
            Description = comparable.Description,
            Notes = comparable.Notes,

            // Template Reference
            TemplateId = comparable.TemplateId,

            // Audit
            CreatedOn = comparable.CreatedOn,
            CreatedBy = comparable.CreatedBy,
            UpdatedOn = comparable.UpdatedOn,
            UpdatedBy = comparable.UpdatedBy,

            // Factor Data
            FactorData = comparable.FactorData.Select(fd => new FactorDataDto
            {
                Id = fd.Id,
                FactorId = fd.FactorId,
                Value = fd.Value,
                OtherRemarks = fd.OtherRemarks
            }).ToList(),

            // Images
            Images = comparable.Images.Select(img => new ImageDto
            {
                Id = img.Id,
                DocumentId = img.DocumentId,
                DisplaySequence = img.DisplaySequence,
                Title = img.Title,
                Description = img.Description
            }).ToList()
        };

        return new GetMarketComparableByIdResult(dto);
    }
}
