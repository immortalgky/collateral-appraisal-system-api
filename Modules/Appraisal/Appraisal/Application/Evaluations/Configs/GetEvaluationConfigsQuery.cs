using Appraisal.Domain.Evaluations;

namespace Appraisal.Application.Evaluations.Configs;

// ── DTO ───────────────────────────────────────────────────────────────────────

public record EvaluationConfigDto(
    Guid    Id,
    string  BankingSegment,
    int     CriteriaSlot,
    string  CriteriaKey,
    string  LabelEn,
    string  LabelTh,
    decimal Weight,
    int     MaxScore,
    string  GuidanceJson,
    string? ThresholdsJson,
    int     DisplayOrder);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetEvaluationConfigsQuery(string BankingSegment)
    : IQuery<List<EvaluationConfigDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetEvaluationConfigsQueryHandler(AppraisalDbContext db)
    : IQueryHandler<GetEvaluationConfigsQuery, List<EvaluationConfigDto>>
{
    public async Task<List<EvaluationConfigDto>> Handle(
        GetEvaluationConfigsQuery query,
        CancellationToken cancellationToken)
    {
        var segment = query.BankingSegment.ToLower();

        return await db.EvaluationCriteriaConfigs
            .AsNoTracking()
            .Where(c => c.BankingSegment.ToLower() == segment)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new EvaluationConfigDto(
                c.Id,
                c.BankingSegment,
                c.CriteriaSlot,
                c.CriteriaKey,
                c.LabelEn,
                c.LabelTh,
                c.Weight,
                c.MaxScore,
                c.GuidanceJson,
                c.ThresholdsJson,
                c.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
