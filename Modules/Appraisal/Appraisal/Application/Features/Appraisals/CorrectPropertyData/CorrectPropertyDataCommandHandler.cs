namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Applies an admin data correction to one property of a closed appraisal.
///
/// Authorization is enforced by the endpoint's "appraisal.data-correction" policy, not by a role
/// check in here — the Collateral module's EditCollateralMaster hardcodes IsInRole("Admin"), which
/// is a pattern to avoid: it cannot be granted or revoked through the admin UI.
/// </summary>
public class CorrectPropertyDataCommandHandler(
    IAppraisalRepository appraisalRepository,
    ICurrentUserService currentUser
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

        var by = currentUser.UserCode ?? currentUser.Username ?? "unknown";

        var outcome = appraisal.CorrectPropertyData(
            command.PropertyId, command.Data, command.Reason, by);

        // An empty correction would write an audit row that says nothing happened. Reject it so the
        // admin knows their edit did not land rather than seeing a success toast.
        if (outcome.ChangedFieldCount == 0)
            throw new BadRequestException("No field values changed.");

        await appraisalRepository.SaveChangesAsync(cancellationToken);

        return new CorrectPropertyDataResult(
            command.AppraisalId,
            command.PropertyId,
            outcome.ChangedFieldCount,
            outcome.ChangedFields);
    }
}
