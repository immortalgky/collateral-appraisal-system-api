namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDetailsByBatch;

public class DeleteSupportingDetailsByBatchCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService) : ICommandHandler<DeleteSupportingDetailsByBatchCommand>
{
    public async Task<Unit> Handle(
        DeleteSupportingDetailsByBatchCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"))
            throw new UnauthorizedAccessException("You are not allowed to remove supporting details.");

        var supportingData = await repo.GetByIdWithDetailsAsync(command.SupportingId, cancellationToken) ?? throw new SupportingDataNotFoundException(command.SupportingId);

        // Status guard — same rule as single-delete
        if (supportingData.Status != SupportingStatus.Draft
         && supportingData.Status != SupportingStatus.RoutedBack)
            throw new DomainException($"Cannot remove details in status '{supportingData.Status}'.");

        // All-or-nothing: RemoveDetail throws SupportingDataDetailNotFoundException
        // if any ID is not found, TransactionalBehavior catches it and rolls back everything
        foreach (var id in command.SupportingDetailIds)
        {
            supportingData.RemoveDetail(id);
        }

        return Unit.Value;
    }
}