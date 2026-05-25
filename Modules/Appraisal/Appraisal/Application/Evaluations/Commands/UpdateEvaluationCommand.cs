using Appraisal.Application.Configurations;
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
    string   EvaluationStatus
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
            evaluatedAt:             dateTimeProvider.ApplicationNow);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
