namespace Appraisal.Application.Features.FeeStructures.CreateFeeStructure;

public record CreateFeeStructureCommand(
    string FeeCode,
    decimal BaseAmount,
    decimal MinSellingPrice,
    decimal? MaxSellingPrice,
    bool IsActive
) : ICommand<FeeStructureDto>, ITransactionalCommand<IAppraisalUnitOfWork>;
