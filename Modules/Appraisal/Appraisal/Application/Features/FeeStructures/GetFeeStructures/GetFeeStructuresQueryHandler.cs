namespace Appraisal.Application.Features.FeeStructures.GetFeeStructures;

public class GetFeeStructuresQueryHandler(AppraisalDbContext db)
    : IQueryHandler<GetFeeStructuresQuery, IReadOnlyList<FeeStructureDto>>
{
    public async Task<IReadOnlyList<FeeStructureDto>> Handle(
        GetFeeStructuresQuery query, CancellationToken ct)
    {
        return await db.FeeStructures
            .AsNoTracking()
            .OrderBy(f => f.FeeCode)
            .ThenBy(f => f.MinSellingPrice)
            .Select(f => new FeeStructureDto(
                f.Id, f.FeeCode, f.BaseAmount, f.MinSellingPrice, f.MaxSellingPrice, f.IsActive))
            .ToListAsync(ct);
    }
}
