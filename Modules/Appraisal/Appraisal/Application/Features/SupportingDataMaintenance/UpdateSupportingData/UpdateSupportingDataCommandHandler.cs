namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingData;

public class UpdateSupportingDataCommandHandler(
    ISupportingDataRepository repo
) : ICommandHandler<UpdateSupportingDataCommand, UpdateSupportingDataResult>
{
    public async Task<UpdateSupportingDataResult> Handle(
        UpdateSupportingDataCommand cmd, CancellationToken ct)
    {
        var supportingData = await repo.GetByIdAsync(cmd.SupportingId, ct)
            ?? throw new SupportingDataNotFoundException(cmd.SupportingId);

        supportingData.Update(new SupportingDataHeader(
            cmd.Header.ImportChannel,
            cmd.Header.ImportDate,
            cmd.Header.SourceOfData,
            cmd.Header.AppraisalCompany,
            cmd.Header.Description,
            cmd.Header.Remark));

        return new UpdateSupportingDataResult(supportingData.Id);
    }
}