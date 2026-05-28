namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateDraftSupportingData;

public class UpdateDraftSupportingDataCommandHandler(
    ISupportingDataRepository repo,
    ICurrentUserService currentUserService
) : ICommandHandler<UpdateDraftSupportingDataCommand, UpdateDraftSupportingDataResult>
{
    public async Task<UpdateDraftSupportingDataResult> Handle(
        UpdateDraftSupportingDataCommand cmd, CancellationToken ct)
    {
        if (currentUserService.IsInRole("IntAppraisalChecker") || currentUserService.IsInRole("ExtAppraisalChecker"))
        {
            throw new UnauthorizedAccessException("Checkers are not allowed to update draft supporting data.");
        }

        var supportingData = await repo.GetByIdAsync(cmd.SupportingId, ct)
            ?? throw new SupportingDataNotFoundException(cmd.SupportingId);

        supportingData.Update(new SupportingDataHeader(
            cmd.Header.ImportChannel,
            cmd.Header.ImportDate,
            cmd.Header.SourceOfData,
            currentUserService.CompanyId,
            cmd.Header.Description,
            cmd.Header.Remark));

        return new UpdateDraftSupportingDataResult(supportingData.Id);
    }
}