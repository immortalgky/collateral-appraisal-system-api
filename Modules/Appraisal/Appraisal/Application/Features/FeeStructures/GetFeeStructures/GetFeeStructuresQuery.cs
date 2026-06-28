namespace Appraisal.Application.Features.FeeStructures.GetFeeStructures;

public record GetFeeStructuresQuery() : IQuery<IReadOnlyList<FeeStructureDto>>;
