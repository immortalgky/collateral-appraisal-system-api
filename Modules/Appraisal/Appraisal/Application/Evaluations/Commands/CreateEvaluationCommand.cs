using Appraisal.Application.Configurations;
using Appraisal.Domain.Evaluations;
using Shared.Identity;

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

public class CreateEvaluationCommandHandler(AppraisalDbContext db, ICurrentUserService currentUser)
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
            note:                    command.Note);

        db.AppraisalEvaluations.Add(evaluation);

        await db.SaveChangesAsync(cancellationToken);

        return new CreateEvaluationResult(evaluation.Id);
    }
}
