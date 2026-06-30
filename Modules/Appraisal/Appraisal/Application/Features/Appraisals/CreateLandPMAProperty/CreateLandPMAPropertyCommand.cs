using Appraisal;
using Appraisal.Application.Features.Appraisals.CreateLandProperty;

public record CreateLandPMAPropertyCommand(
    Guid AppraisalId,
    Guid? GroupId,
    decimal? SellingPrice,
    decimal? ForcedSalePrice,
    decimal? BuildingInsurancePrice,
    List<LandTitleItemData>? Titles = null,
    string? SubDistrict = null,
    string? District = null,
    string? Province = null
) : ICommand<CreateLandPMAPropertyResult>, ITransactionalCommand<IAppraisalUnitOfWork>;