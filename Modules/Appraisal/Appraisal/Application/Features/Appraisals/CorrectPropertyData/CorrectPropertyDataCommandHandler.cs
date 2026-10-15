using System.Text.Json;
using Appraisal.Infrastructure;
using Microsoft.Extensions.Options;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Applies an admin correction to one property of a Completed appraisal: snapshot, apply the payload with
/// the same applier the real page handler uses, flush, snapshot again, diff, and write one audit row — all
/// in the transaction TransactionalBehavior opens, so the change and its audit row cannot diverge.
///
/// Authorization is enforced by the endpoint's "appraisal.data-correction" policy, not by a role check in
/// here — the Collateral module's EditCollateralMaster hardcodes IsInRole("Admin"), which is a pattern to
/// avoid: it cannot be granted or revoked through the admin UI.
///
/// Deliberately never calls AppraisalValuationSummaryService.RecomputeAsync and never touches the pricing
/// analyses: a correction keeps every approved figure exactly as approved. (The real pages recompute; that
/// would overwrite the reviewer's approved values and publish AppraisalValueChanged to the workflow.)
/// </summary>
public class CorrectPropertyDataCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    ISender mediator,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions
) : ICommandHandler<CorrectPropertyDataCommand, CorrectPropertyDataResult>
{
    public async Task<CorrectPropertyDataResult> Handle(
        CorrectPropertyDataCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // Completed only. Restricting this path keeps it from becoming a way around the workflow's
        // own validation on in-flight work, and a Cancelled appraisal is abandoned — correcting its
        // descriptive data serves no purpose, so it stays read-only like everything else.
        if (appraisal.Status != AppraisalStatus.Completed)
        {
            throw new ConflictException(
                $"Appraisal is {appraisal.Status.Code}. Data correction applies to Completed " +
                "appraisals only.",
                // Machine-readable so clients don't have to substring-match the message, which
                // would break the moment the wording changes.
                "APPRAISAL_NOT_COMPLETED");
        }

        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        var target = PropertyCorrectionTargets.Find(command.Suffix)
                     ?? throw new BadRequestException($"'{command.Suffix}' is not a correctable property route.");

        if (property.PropertyType != target.Type)
            throw new BadRequestException(
                $"'{command.Suffix}' does not apply to a {property.PropertyType.Code} property.");

        var before = PropertySnapshot.Take(property);

        await target.Apply(new CorrectionInput(
            property, command.Data, jsonOptions.Value.SerializerOptions, mediator, cancellationToken));

        // Flush first: rows created by the payload only get their ids here, and the after-snapshot has to
        // describe what was actually stored. Still inside the caller's transaction, so a NO_CHANGES below
        // (or any failure) rolls this back.
        await appraisalRepository.SaveChangesAsync(cancellationToken);

        var changes = SnapshotDiff.Compare(before, PropertySnapshot.Take(property));

        // An empty correction would write an audit row that says nothing happened. Reject it so the
        // admin knows their edit did not land rather than seeing a success toast.
        if (changes.Count == 0)
            throw new BadRequestException("No field values changed.", null, "NO_CHANGES");

        dbContext.AppraisalPropertyCorrectionLogs.Add(new AppraisalPropertyCorrectionLog(
            command.AppraisalId,
            command.PropertyId,
            property.PropertyType.Code,
            JsonSerializer.Serialize(changes),
            command.Reason.Trim(),
            currentUser.UserCode ?? currentUser.Username ?? "unknown",
            dateTimeProvider.ApplicationNow));

        return new CorrectPropertyDataResult(changes.Count, changes.Keys.ToList());
    }
}
