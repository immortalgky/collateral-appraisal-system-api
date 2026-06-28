using MediatR;

namespace Appraisal.Application.Features.FeeStructures.DeleteFeeStructure;

public class DeleteFeeStructureCommandHandler(AppraisalDbContext db)
    : ICommandHandler<DeleteFeeStructureCommand, Unit>
{
    public async Task<Unit> Handle(DeleteFeeStructureCommand cmd, CancellationToken ct)
    {
        var entity = await db.FeeStructures.FindAsync([cmd.Id], ct)
            ?? throw new NotFoundException("FeeStructure", cmd.Id);

        db.FeeStructures.Remove(entity);
        // No SaveChangesAsync — TransactionalBehavior commits the unit of work.

        return Unit.Value;
    }
}
