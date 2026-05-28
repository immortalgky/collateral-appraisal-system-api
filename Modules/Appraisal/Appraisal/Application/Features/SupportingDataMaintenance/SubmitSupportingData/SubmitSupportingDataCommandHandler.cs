namespace Appraisal.Application.Features.SupportingDataMaintenance.SubmitSupportingData;

public class SubmitSupportingDataCommandHandler(
    ISupportingDataRepository repo,
    ICurrentUserService currentUserService
) : ICommandHandler<SubmitSupportingDataCommand, SubmitSupportingDataResult>
{
    public async Task<SubmitSupportingDataResult> Handle(
        SubmitSupportingDataCommand cmd, CancellationToken ct)
    {
        // A non-empty Decision means this call is a checker action
        // (Approve / Reject / Route back / Cancel). In that case we must
        // NOT touch the header fields — only the status transition matters.
        var isCheckerDecision = !string.IsNullOrWhiteSpace(cmd.Header.Decision);

        SupportingData supportingData;
        if (isCheckerDecision)
        {
            // Checker path: record must already exist; header is ignored.
            if (cmd.SupportingId is null)
                throw new DomainException("SupportingId is required when submitting a decision.");

            supportingData = await repo.GetByIdAsync(cmd.SupportingId.Value, ct)
                ?? throw new SupportingDataNotFoundException(cmd.SupportingId.Value);
        }
        else if (cmd.SupportingId is null)
        {
            // Creator path, brand-new record: create from the supplied header.
            supportingData = SupportingData.Create(new SupportingDataHeader(
                cmd.Header.ImportChannel,
                cmd.Header.ImportDate,
                cmd.Header.SourceOfData,
                currentUserService.CompanyId,
                cmd.Header.Description,
                cmd.Header.Remark));

            await repo.AddAsync(supportingData, ct);
        }
        else
        {
            // Creator path, existing draft: refresh the header before submitting.
            supportingData = await repo.GetByIdAsync(cmd.SupportingId.Value, ct)
                ?? throw new SupportingDataNotFoundException(cmd.SupportingId.Value);

            supportingData.Update(new SupportingDataHeader(
                cmd.Header.ImportChannel,
                cmd.Header.ImportDate,
                cmd.Header.SourceOfData,
                currentUserService.CompanyId,
                cmd.Header.Description,
                cmd.Header.Remark));
        }

        supportingData.Submit(cmd.Header.Decision, cmd.Header.Remark);
        return new SubmitSupportingDataResult(true);
    }
}