using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;

namespace Appraisal.Application.Features.FeeStructures.CreateFeeStructure;

public class CreateFeeStructureCommandHandler(
    AppraisalDbContext db,
    IParameterLookupService parameterLookup)
    : ICommandHandler<CreateFeeStructureCommand, FeeStructureDto>
{
    // Fee codes are maintained in the TypeOfFee parameter group.
    private const string FeeTypeParameterGroup = "TypeOfFee";

    public async Task<FeeStructureDto> Handle(CreateFeeStructureCommand cmd, CancellationToken ct)
    {
        var feeCode = cmd.FeeCode.Trim();

        // The fee code must exist in the TypeOfFee parameter group — otherwise it has no
        // resolvable display name and tier matching keys off a meaningless code.
        var validCodes = await parameterLookup.GetValidCodesAsync(
            new ParameterDto(null, FeeTypeParameterGroup, null, null, feeCode, null, true, null), ct);
        if (!validCodes.Contains(feeCode))
            throw new BadRequestException($"Fee code '{feeCode}' is not a valid {FeeTypeParameterGroup} code.");

        await FeeStructureMapping.EnsureNoActiveOverlapAsync(
            db, feeCode, cmd.MinSellingPrice, cmd.MaxSellingPrice, cmd.IsActive, excludeId: null, ct);

        var entity = FeeStructure.Create(
            feeCode, cmd.BaseAmount, cmd.MinSellingPrice, cmd.MaxSellingPrice, cmd.IsActive);

        db.FeeStructures.Add(entity);
        // No SaveChangesAsync — TransactionalBehavior commits the unit of work.

        return entity.ToDto();
    }
}
