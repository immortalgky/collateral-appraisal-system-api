
namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDataById;

public class DeleteSupportingDataByIdCommandHandler(ISupportingDataRepository repo)
    : ICommandHandler<DeleteSupportingDataByIdCommand>
{
    public async Task<Unit> Handle(
        DeleteSupportingDataByIdCommand command,
        CancellationToken cancellationToken)
    {
        var supportingData = await repo.GetByIdWithDetailsAsync(command.SupportingId, cancellationToken) ?? throw new SupportingDataNotFoundException(command.SupportingId);

        await repo.DeleteAsync(supportingData, cancellationToken);

        // No SaveChangesAsync - TransactionalBehavior handles it.
        return Unit.Value;
    }
}