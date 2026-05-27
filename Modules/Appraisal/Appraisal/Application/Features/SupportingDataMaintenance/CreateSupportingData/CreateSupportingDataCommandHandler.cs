namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingData;

internal class CreateSupportingDataCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : ICommandHandler<CreateSupportingDataCommand, CreateSupportingDataResult>
{
    public async Task<CreateSupportingDataResult> Handle(
        CreateSupportingDataCommand cmd, CancellationToken ct)
    {
        if (currentUserService.IsInRole("IntAppraisalChecker") || currentUserService.IsInRole("ExtAppraisalChecker"))
        {
            throw new UnauthorizedAccessException("Checkers are not allowed to create supporting data.");
        }

        var entity = SupportingData.Create(new SupportingDataHeader(
            cmd.Header.ImportChannel,
            cmd.Header.ImportDate,
            cmd.Header.SourceOfData,
            cmd.Header.AppraisalCompany,
            cmd.Header.Description,
            cmd.Header.Remark));

        await repo.AddAsync(entity, ct);
        // No SaveChangesAsync — TransactionalBehavior handles it.

        return new CreateSupportingDataResult(entity.Id);
    }
}