namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateDraftSupportingData;

public class UpdateDraftSupportingDataCommandHandler(
    ISupportingDataRepository repo,
    ICurrentUserService currentUserService
) : ICommandHandler<UpdateDraftSupportingDataCommand, UpdateDraftSupportingDataResult>
{
    public async Task<UpdateDraftSupportingDataResult> Handle(
        UpdateDraftSupportingDataCommand cmd, CancellationToken ct)
    {
        if (!currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"))
            throw new UnauthorizedAccessException("You are not allowed to edit supporting data.");

        var supportingData = await repo.GetByIdAsync(cmd.SupportingId, ct)
            ?? throw new SupportingDataNotFoundException(cmd.SupportingId);

        supportingData.Update(new SupportingDataHeader(
            cmd.Header.ImportChannel,
            cmd.Header.ImportDate,
            cmd.Header.SourceOfData,
            cmd.Header.AppraisalCompanyId,
            cmd.Header.Description,
            cmd.Header.Remark));

        return new UpdateDraftSupportingDataResult(supportingData.Id);
    }
}