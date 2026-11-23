using Appraisal.Application.Configurations;
using Appraisal.Domain.Evaluations;
using Shared.Identity;
using Shared.Time;

namespace Appraisal.Application.Evaluations.Commands;

public record UpdateEvaluationCommand(
    Guid     Id,
    int?     Criteria1Rating,
    int?     Criteria2Rating,
    bool     Criteria2IsAutoDetected,
    decimal? Criteria2DetectedDays,
    int?     Criteria3Rating,
    int?     Criteria4Rating,
    int?     Criteria5Rating,
    string?  AdditionalComments,
    string?  Note,
    string   EvaluationStatus,
    string?  Criteria1Remark,
    string?  Criteria2Remark,
    string?  Criteria3Remark,
    string?  Criteria4Remark,
    string?  Criteria5Remark
) : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;

public class UpdateEvaluationCommandHandler(
    AppraisalDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateEvaluationCommand, Unit>
{
    public async Task<Unit> Handle(
        UpdateEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        var evaluation = await db.AppraisalEvaluations
            .FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(
                $"AppraisalEvaluation with id '{command.Id}' was not found.");

        // Snapshot the composite score using the weights in force *now* — only on the
        // Pending → Completed transition. Pending rows are scored live by the view.
        decimal? totalScore = null;
        if (command.EvaluationStatus == "Completed")
        {
            var weights = await EvaluationWeights.LoadAsync(db, evaluation.AppraisalId, cancellationToken);
            totalScore = AppraisalEvaluation.ComputeScore(
                weights,
                command.Criteria1Rating, command.Criteria2Rating, command.Criteria3Rating,
                command.Criteria4Rating, command.Criteria5Rating);
        }

        evaluation.Update(
            criteria1Rating:         command.Criteria1Rating,
            criteria2Rating:         command.Criteria2Rating,
            criteria2IsAutoDetected: command.Criteria2IsAutoDetected,
            criteria2DetectedDays:   command.Criteria2DetectedDays,
            criteria3Rating:         command.Criteria3Rating,
            criteria4Rating:         command.Criteria4Rating,
            criteria5Rating:         command.Criteria5Rating,
            additionalComments:      command.AdditionalComments,
            note:                    command.Note,
            evaluationStatus:        command.EvaluationStatus,
            evaluatedBy:             currentUser.Username,
            evaluatedAt:             dateTimeProvider.ApplicationNow,
            totalScore:              totalScore,
            criteria1Remark:         command.Criteria1Remark,
            criteria2Remark:         command.Criteria2Remark,
            criteria3Remark:         command.Criteria3Remark,
            criteria4Remark:         command.Criteria4Remark,
            criteria5Remark:         command.Criteria5Remark);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
