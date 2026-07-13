using Appraisal.Application.Features.Appraisals.CreateLandProperty;

public record GetLandPMAPropertyResponse
{
    public Guid PropertyId { get; init; }
    public Guid AppraisalId { get; init; }
    public decimal? SellingPrice { get; init; }
    public decimal? ForcedSalePrice { get; init; }
    public decimal? BuildingInsurancePrice { get; init; }
    public string ExternalSyncStatus { get; init; }
    public string? ExternalSyncError { get; init; }
    public DateTime? ExternalSyncedAt { get; init; }
    public string? SubDistrict { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }
    public List<LandTitleItemData>? Titles { get; init; }

}