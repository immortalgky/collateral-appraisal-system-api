
namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDataById;

public class DeleteSupportingDataByIdCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : ICommandHandler<DeleteSupportingDataByIdCommand>
{
    private static readonly HashSet<string> RemovableStatuses =
        new(StringComparer.Ordinal) { "Draft", "Approved", "Rejected", "Cancelled" };

    public async Task<Unit> Handle(
        DeleteSupportingDataByIdCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.HasPermission("SUPPORTING_DATA_MAINT_REMOVE"))
            throw new UnauthorizedAccessException("You are not allowed to remove supporting data.");

        var supportingData = await repo.GetByIdWithDetailsAsync(command.SupportingId, cancellationToken) ?? throw new SupportingDataNotFoundException(command.SupportingId);

        if (!RemovableStatuses.Contains(supportingData.Status.Code))
            throw new DomainException($"Cannot remove supporting data in status '{supportingData.Status}'.");

        await repo.DeleteAsync(supportingData, cancellationToken);

        // No SaveChangesAsync - TransactionalBehavior handles it.
        return Unit.Value;
    }
}