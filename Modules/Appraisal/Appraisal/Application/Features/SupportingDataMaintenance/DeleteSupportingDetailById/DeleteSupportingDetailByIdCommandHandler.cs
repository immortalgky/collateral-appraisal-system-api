
namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDetailById;

public class DeleteSupportingDetailByIdCommandHandler(ISupportingDataRepository repo)
    : ICommandHandler<DeleteSupportingDetailByIdCommand>
{
    public async Task<Unit> Handle(
        DeleteSupportingDetailByIdCommand command,
        CancellationToken cancellationToken)
    {
        var supportingData = await repo.GetByIdWithDetailsAsync(command.SupportingId, cancellationToken) ?? throw new SupportingDataNotFoundException(command.SupportingId);

        supportingData.RemoveDetail(command.DetailId);
        // No SaveChangesAsync - TransactionalBehavior handles it.

        return Unit.Value;
    }
}