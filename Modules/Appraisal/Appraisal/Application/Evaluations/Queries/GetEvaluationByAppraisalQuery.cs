using Appraisal.Domain.Evaluations;

namespace Appraisal.Application.Evaluations.Queries;

public record GetEvaluationByAppraisalQuery(Guid AppraisalId)
    : IQuery<AppraisalEvaluationDetail?>;

/// <summary>Full evaluation detail returned to the caller.</summary>
public record AppraisalEvaluationDetail(
    Guid      Id,
    Guid      AppraisalId,
    string    AppraisalNumber,
    string    EvaluationStatus,
    string?   EvaluatedBy,
    DateTime? EvaluatedAt,
    int?      Criteria1Rating,
    int?      Criteria2Rating,
    bool      Criteria2IsAutoDetected,
    decimal?  Criteria2DetectedDays,
    int?      Criteria3Rating,
    int?      Criteria4Rating,
    int?      Criteria5Rating,
    string?   AdditionalComments,
    string?   Note,
    DateTime? CreatedAt,
    string?   CreatedBy,
    DateTime? UpdatedAt,
    string?   UpdatedBy);

public class GetEvaluationByAppraisalQueryHandler(AppraisalDbContext db)
    : IQueryHandler<GetEvaluationByAppraisalQuery, AppraisalEvaluationDetail?>
{
    public async Task<AppraisalEvaluationDetail?> Handle(
        GetEvaluationByAppraisalQuery query,
        CancellationToken cancellationToken)
    {
        var e = await db.AppraisalEvaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ev => ev.AppraisalId == query.AppraisalId,
                cancellationToken);

        if (e is null)
            return null;

        return new AppraisalEvaluationDetail(
            Id:                      e.Id,
            AppraisalId:             e.AppraisalId,
            AppraisalNumber:         e.AppraisalNumber,
            EvaluationStatus:        e.EvaluationStatus,
            EvaluatedBy:             e.EvaluatedBy,
            EvaluatedAt:             e.EvaluatedAt,
            Criteria1Rating:         e.Criteria1Rating,
            Criteria2Rating:         e.Criteria2Rating,
            Criteria2IsAutoDetected: e.Criteria2IsAutoDetected,
            Criteria2DetectedDays:   e.Criteria2DetectedDays,
            Criteria3Rating:         e.Criteria3Rating,
            Criteria4Rating:         e.Criteria4Rating,
            Criteria5Rating:         e.Criteria5Rating,
            AdditionalComments:      e.AdditionalComments,
            Note:                    e.Note,
            CreatedAt:               e.CreatedAt,
            CreatedBy:               e.CreatedBy,
            UpdatedAt:               e.UpdatedAt,
            UpdatedBy:               e.UpdatedBy);
    }
}
