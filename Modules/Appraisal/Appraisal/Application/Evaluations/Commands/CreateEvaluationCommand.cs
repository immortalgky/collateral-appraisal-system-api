using Appraisal.Application.Configurations;
using Appraisal.Domain.Evaluations;
using Shared.Identity;
using Shared.Time;

namespace Appraisal.Application.Evaluations.Commands;

public record CreateEvaluationCommand(
    Guid     AppraisalId,
    string   EvaluationStatus,
    int?     Criteria1Rating,
    int?     Criteria2Rating,
    bool     Criteria2IsAutoDetected,
    decimal? Criteria2DetectedDays,
    int?     Criteria3Rating,
    int?     Criteria4Rating,
    int?     Criteria5Rating,
    string?  AdditionalComments,
    string?  Note
) : ICommand<CreateEvaluationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record CreateEvaluationResult(Guid Id);

public class CreateEvaluationCommandHandler(
    AppraisalDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateEvaluationCommand, CreateEvaluationResult>
{
    public async Task<CreateEvaluationResult> Handle(
        CreateEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        // Enforce 1:1 — no duplicate evaluations per appraisal
        var alreadyExists = await db.AppraisalEvaluations
            .AnyAsync(e => e.AppraisalId == command.AppraisalId, cancellationToken);

        if (alreadyExists)
            throw new InvalidOperationException(
                $"An evaluation already exists for appraisal {command.AppraisalId}.");

        // Look up AppraisalNumber server-side — client should not send it
        var appraisalNumber = await db.Appraisals
            .Where(a => a.Id == command.AppraisalId)
            .Select(a => a.AppraisalNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? command.AppraisalId.ToString();

        // Snapshot the composite score using the weights in force *now* — only when the
        // evaluation is completed on create. Pending rows are scored live by the view.
        decimal? totalScore = null;
        if (command.EvaluationStatus == "Completed")
        {
            var weights = await EvaluationWeights.LoadAsync(db, command.AppraisalId, cancellationToken);
            totalScore = AppraisalEvaluation.ComputeScore(
                weights,
                command.Criteria1Rating, command.Criteria2Rating, command.Criteria3Rating,
                command.Criteria4Rating, command.Criteria5Rating);
        }

        var evaluation = AppraisalEvaluation.Create(
            appraisalId:             command.AppraisalId,
            appraisalNumber:         appraisalNumber,
            evaluationStatus:        command.EvaluationStatus,
            evaluatedBy:             command.EvaluationStatus == "Completed" ? currentUser.Username : null,
            criteria1Rating:         command.Criteria1Rating,
            criteria2Rating:         command.Criteria2Rating,
            criteria2IsAutoDetected: command.Criteria2IsAutoDetected,
            criteria2DetectedDays:   command.Criteria2DetectedDays,
            criteria3Rating:         command.Criteria3Rating,
            criteria4Rating:         command.Criteria4Rating,
            criteria5Rating:         command.Criteria5Rating,
            additionalComments:      command.AdditionalComments,
            note:                    command.Note,
            evaluatedAt:             dateTimeProvider.ApplicationNow,
            totalScore:              totalScore);

        db.AppraisalEvaluations.Add(evaluation);

        await db.SaveChangesAsync(cancellationToken);

        return new CreateEvaluationResult(evaluation.Id);
    }
}
