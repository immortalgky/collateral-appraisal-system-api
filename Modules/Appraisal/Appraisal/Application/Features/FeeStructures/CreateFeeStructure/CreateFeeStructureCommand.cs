namespace Appraisal.Application.Features.FeeStructures.CreateFeeStructure;

public record CreateFeeStructureCommand(
    string FeeCode,
    decimal BaseAmount,
    decimal MinSellingPrice,
    decimal? MaxSellingPrice,
    bool IsActive,
    // Null (or omitted) creates a tier on the generic ladder that applies to any appraisal type.
    string? AppraisalType = null
) : ICommand<FeeStructureDto>, ITransactionalCommand<IAppraisalUnitOfWork>;
