
namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDetailById;

public class DeleteSupportingDetailByIdCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : ICommandHandler<DeleteSupportingDetailByIdCommand>
{
    public async Task<Unit> Handle(
        DeleteSupportingDetailByIdCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"))
            throw new UnauthorizedAccessException("You are not allowed to remove supporting details.");

        var supportingData = await repo.GetByIdWithDetailsAsync(command.SupportingId, cancellationToken) ?? throw new SupportingDataNotFoundException(command.SupportingId);

        if (supportingData.Status != SupportingStatus.Draft
         && supportingData.Status != SupportingStatus.RoutedBack)
            throw new DomainException($"Cannot remove detail in status '{supportingData.Status}'.");

        supportingData.RemoveDetail(command.DetailId);
        // No SaveChangesAsync - TransactionalBehavior handles it.

        return Unit.Value;
    }
}