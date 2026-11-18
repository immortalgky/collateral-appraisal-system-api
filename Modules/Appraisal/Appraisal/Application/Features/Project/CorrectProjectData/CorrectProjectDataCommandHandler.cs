using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;
using Microsoft.Extensions.Options;
using Shared.Time;

namespace Appraisal.Application.Features.Project.CorrectProjectData;

public class CorrectProjectDataCommandHandler(
    IAppraisalRepository appraisalRepository,
    IProjectRepository projectRepository,
    AppraisalDbContext dbContext,
    ISender mediator,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions
) : ICommandHandler<CorrectProjectDataCommand, CorrectPropertyDataResult>
{
    public async Task<CorrectPropertyDataResult> Handle(
        CorrectProjectDataCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        if (appraisal.Status != AppraisalStatus.Completed)
        {
            throw new ConflictException(
                $"Appraisal is {appraisal.Status.Code}. Data correction applies to Completed " +
                "appraisals only.",
                "APPRAISAL_NOT_COMPLETED");
        }

        var project = await projectRepository.GetByAppraisalIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Project", command.AppraisalId);

        var target = ProjectCorrectionTargets.Get(command.Target);

        var before = await ProjectSnapshot.TakeAsync(
            dbContext, project.Id, command.Target, command.TargetId, cancellationToken);

        await target.Apply(new ProjectCorrectionInput(
            command, project, jsonOptions.Value.SerializerOptions, mediator, cancellationToken));

        await dbContext.SaveChangesAsync(cancellationToken);

        var after = await ProjectSnapshot.TakeAsync(
            dbContext, project.Id, command.Target, command.TargetId, cancellationToken);

        var changes = SnapshotDiff.Compare(before, after);
        if (changes.Count == 0)
            throw new BadRequestException("No field values changed.", null, "NO_CHANGES");

        dbContext.AppraisalPropertyCorrectionLogs.Add(AppraisalPropertyCorrectionLog.ForProject(
            command.AppraisalId,
            target.EntryType,
            ProjectCorrectionTargets.NeedsId(command.Target) ? command.TargetId : null,
            JsonSerializer.Serialize(changes),
            command.Reason.Trim(),
            currentUser.UserCode ?? currentUser.Username ?? "unknown",
            dateTimeProvider.ApplicationNow));

        return new CorrectPropertyDataResult(changes.Count, changes.Keys.ToList());
    }
}
