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
    int       Criteria1Rating,
    string?   Criteria1Description,
    int       Criteria2Rating,
    bool      Criteria2IsAutoDetected,
    decimal?  Criteria2DetectedDays,
    string?   Criteria2Description,
    int       Criteria3Rating,
    string?   Criteria3Description,
    int       Criteria4Rating,
    string?   Criteria4Description,
    int       Criteria5Rating,
    string?   Criteria5Description,
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
            Criteria1Description:    e.Criteria1Description,
            Criteria2Rating:         e.Criteria2Rating,
            Criteria2IsAutoDetected: e.Criteria2IsAutoDetected,
            Criteria2DetectedDays:   e.Criteria2DetectedDays,
            Criteria2Description:    e.Criteria2Description,
            Criteria3Rating:         e.Criteria3Rating,
            Criteria3Description:    e.Criteria3Description,
            Criteria4Rating:         e.Criteria4Rating,
            Criteria4Description:    e.Criteria4Description,
            Criteria5Rating:         e.Criteria5Rating,
            Criteria5Description:    e.Criteria5Description,
            AdditionalComments:      e.AdditionalComments,
            Note:                    e.Note,
            CreatedAt:               e.CreatedAt,
            CreatedBy:               e.CreatedBy,
            UpdatedAt:               e.UpdatedAt,
            UpdatedBy:               e.UpdatedBy);
    }
}
