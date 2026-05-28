namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateDraftSupportingData;

internal class CreateDraftSupportingDataCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : ICommandHandler<CreateDraftSupportingDataCommand, CreateDraftSupportingDataResult>
{
    public async Task<CreateDraftSupportingDataResult> Handle(
        CreateDraftSupportingDataCommand cmd, CancellationToken ct)
    {
        if (currentUserService.IsInRole("IntAppraisalChecker") || currentUserService.IsInRole("ExtAppraisalChecker"))
        {
            throw new UnauthorizedAccessException("Checkers are not allowed to create draft supporting data.");
        }

        var entity = SupportingData.Create(new SupportingDataHeader(
            cmd.Header.ImportChannel,
            cmd.Header.ImportDate,
            cmd.Header.SourceOfData,
            currentUserService.CompanyId,
            cmd.Header.Description,
            cmd.Header.Remark));

        await repo.AddAsync(entity, ct);
        // No SaveChangesAsync — TransactionalBehavior handles it.

        return new CreateDraftSupportingDataResult(entity.Id);
    }
}