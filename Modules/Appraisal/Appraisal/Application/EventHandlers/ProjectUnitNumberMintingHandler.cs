using Appraisal.Application.Services;
using Appraisal.Domain.Projects;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Issues unit numbers for a block project's units at the moment its appraisal is approved.
///
/// WHY APPROVAL AND NOT UPLOAD. Until the appraisal is approved the unit list is still in flux —
/// a user who uploads the wrong workbook simply uploads the right one, and work routed back for
/// correction is re-uploaded too. Numbering at upload time would burn a block of numbers on every
/// one of those attempts. Numbering at approval means a draft costs nothing.
///
/// WHY A DOMAIN-EVENT HANDLER. AppraisalCompletedEvent is raised from both approval paths —
/// MarkApprovedByCommittee (committee approval) and Complete (review approval) — so one handler
/// covers both. It runs inside the same SaveChanges as the approval itself, because
/// DispatchDomainEventInterceptor dispatches before the write is issued; the numbers and the
/// approval therefore commit or roll back together.
///
/// IDEMPOTENT. Only units with no number are given one, so a retried delivery or a second
/// approval of the same appraisal is a no-op. A block reappraisal is a different appraisal with a
/// different set of unit rows, so its units are numbered afresh — deliberately: continuity between
/// rounds lives in Appraisal.PrevAppraisalId, exactly as it does for appraisal numbers themselves.
/// </summary>
public class ProjectUnitNumberMintingHandler(
    IProjectRepository projectRepository,
    IProjectUnitNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider,
    ILogger<ProjectUnitNumberMintingHandler> logger) : INotificationHandler<AppraisalCompletedEvent>
{
    public async Task Handle(AppraisalCompletedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        // Project is its own aggregate — Appraisal does not navigate to it. A null result means
        // this is an ordinary appraisal, which has no units to number.
        var project = await projectRepository.GetWithUnitsByAppraisalIdAsync(appraisal.Id, cancellationToken);
        if (project is null)
            return;

        var pending = project.CountUnitsAwaitingNumber();
        if (pending == 0)
            return;

        // The Buddhist year the numbers belong to is the year of approval. CompletedAt is stamped
        // by the same call that raised this event, so it is set by now; the fallback only guards
        // against a future path that raises the event without stamping it. It uses the application
        // clock, not machine local time — on a UTC host an approval just after local midnight on
        // 31 December would otherwise be numbered into the wrong year's series.
        var completedAt = appraisal.CompletedAt ?? dateTimeProvider.ApplicationNow;
        var thaiYear = completedAt.Year + 543;

        var numbers = await numberGenerator.GenerateAsync(thaiYear, pending, cancellationToken);
        project.AssignUnitNumbers(numbers);

        logger.LogInformation(
            "Issued {Count} unit number(s) ({First}..{Last}) for project {ProjectId} on approval of appraisal {AppraisalId}.",
            numbers.Count, numbers[0], numbers[^1], project.Id, appraisal.Id);
    }
}
