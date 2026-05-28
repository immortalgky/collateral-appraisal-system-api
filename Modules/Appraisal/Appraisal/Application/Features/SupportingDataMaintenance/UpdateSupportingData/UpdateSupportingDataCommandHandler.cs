namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingData;

public class UpdateSupportingDataCommandHandler(
    ISupportingDataRepository repo,
    ICurrentUserService currentUserService
) : ICommandHandler<UpdateSupportingDataCommand, UpdateSupportingDataResult>
{
    public async Task<UpdateSupportingDataResult> Handle(
        UpdateSupportingDataCommand cmd, CancellationToken ct)
    {
        if (currentUserService.IsInRole("IntAppraisalChecker") || currentUserService.IsInRole("ExtAppraisalChecker"))
        {
            throw new UnauthorizedAccessException("Checkers are not allowed to update supporting data.");
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

        return new UpdateSupportingDataResult(supportingData.Id);
    }
}