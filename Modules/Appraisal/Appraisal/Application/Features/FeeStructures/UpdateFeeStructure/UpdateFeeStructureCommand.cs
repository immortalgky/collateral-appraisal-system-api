namespace Appraisal.Application.Features.FeeStructures.UpdateFeeStructure;

// FeeCode is immutable — it identifies which fee family the tier belongs to.
public record UpdateFeeStructureCommand(
    Guid Id,
    decimal BaseAmount,
    decimal MinSellingPrice,
    decimal? MaxSellingPrice,
    bool IsActive
) : ICommand<FeeStructureDto>, ITransactionalCommand<IAppraisalUnitOfWork>;
