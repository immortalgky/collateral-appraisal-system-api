using Appraisal.Application.Configurations;
using Shared.Identity;

namespace Appraisal.Application.Evaluations.Commands;

public record UpdateEvaluationCommand(
    Guid     Id,
    int      Criteria1Rating,
    string?  Criteria1Description,
    int      Criteria2Rating,
    bool     Criteria2IsAutoDetected,
    decimal? Criteria2DetectedDays,
    string?  Criteria2Description,
    int      Criteria3Rating,
    string?  Criteria3Description,
    int      Criteria4Rating,
    string?  Criteria4Description,
    int      Criteria5Rating,
    string?  Criteria5Description,
    string?  AdditionalComments,
    string?  Note,
    string   EvaluationStatus
) : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;

public class UpdateEvaluationCommandHandler(AppraisalDbContext db, ICurrentUserService currentUser)
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
            criteria1Description:    command.Criteria1Description,
            criteria2Rating:         command.Criteria2Rating,
            criteria2IsAutoDetected: command.Criteria2IsAutoDetected,
            criteria2DetectedDays:   command.Criteria2DetectedDays,
            criteria2Description:    command.Criteria2Description,
            criteria3Rating:         command.Criteria3Rating,
            criteria3Description:    command.Criteria3Description,
            criteria4Rating:         command.Criteria4Rating,
            criteria4Description:    command.Criteria4Description,
            criteria5Rating:         command.Criteria5Rating,
            criteria5Description:    command.Criteria5Description,
            additionalComments:      command.AdditionalComments,
            note:                    command.Note,
            evaluationStatus:        command.EvaluationStatus,
            evaluatedBy:             currentUser.Username);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
