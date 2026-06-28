namespace Appraisal.Application.Features.FeeStructures.UpdateFeeStructure;

public class UpdateFeeStructureCommandHandler(AppraisalDbContext db)
    : ICommandHandler<UpdateFeeStructureCommand, FeeStructureDto>
{
    public async Task<FeeStructureDto> Handle(UpdateFeeStructureCommand cmd, CancellationToken ct)
    {
        var entity = await db.FeeStructures.FindAsync([cmd.Id], ct)
            ?? throw new NotFoundException("FeeStructure", cmd.Id);

        await FeeStructureMapping.EnsureNoActiveOverlapAsync(
            db, entity.FeeCode, cmd.MinSellingPrice, cmd.MaxSellingPrice, cmd.IsActive, excludeId: cmd.Id, ct);

        entity.Update(cmd.BaseAmount, cmd.MinSellingPrice, cmd.MaxSellingPrice, cmd.IsActive);
        // No SaveChangesAsync — TransactionalBehavior commits the unit of work.

        return entity.ToDto();
    }
}
