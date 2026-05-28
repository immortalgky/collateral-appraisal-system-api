namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateDraftSupportingData;

internal class CreateDraftSupportingDataCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : ICommandHandler<CreateDraftSupportingDataCommand, CreateDraftSupportingDataResult>
{
    public async Task<CreateDraftSupportingDataResult> Handle(
        CreateDraftSupportingDataCommand cmd, CancellationToken ct)
    {
        // 1. Permission check
        var hasEditPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT");
        if (!hasEditPermission)
        {
            throw new UnauthorizedAccessException("You are not allowed to create draft supporting data.");
        }

        // 2. Create new entity (with header info only; details will be added later)
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