using Appraisal;
using Appraisal.Application.Features.Appraisals.CreateLandProperty;

public record UpdateLandPMAPropertyCommand(
    Guid AppraisalId,
    Guid PropertyId,
    decimal? SellingPrice,
    decimal? ForcedSalePrice,
    decimal? BuildingInsurancePrice,
    List<LandTitleItemData>? Titles = null,
    string? SubDistrict = null,
    string? District = null,
    string? Province = null
) :ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;