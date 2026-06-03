using Appraisal.Application.Configurations;

namespace Appraisal.Application.Evaluations.Configs;

// ── Command ───────────────────────────────────────────────────────────────────

public record UpdateEvaluationConfigCommand(
    Guid    Id,
    string  LabelEn,
    string  LabelTh,
    decimal Weight,
    int     MaxScore,
    string  GuidanceJson,
    string? ThresholdsJson)
    : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class UpdateEvaluationConfigCommandHandler(AppraisalDbContext db)
    : ICommandHandler<UpdateEvaluationConfigCommand, Unit>
{
    public async Task<Unit> Handle(
        UpdateEvaluationConfigCommand command,
        CancellationToken cancellationToken)
    {
        var config = await db.EvaluationCriteriaConfigs
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(
                $"EvaluationCriteriaConfig with id '{command.Id}' was not found.");

        config.Update(
            labelEn:       command.LabelEn,
            labelTh:       command.LabelTh,
            weight:        command.Weight,
            maxScore:      command.MaxScore,
            guidanceJson:  command.GuidanceJson,
            thresholdsJson: command.ThresholdsJson);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
