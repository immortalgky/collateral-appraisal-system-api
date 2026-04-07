using Appraisal.Application.Features.BlockVillage.GetVillageModels;

namespace Appraisal.Application.Features.BlockVillage.GetVillageModelById;

public class GetVillageModelByIdQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetVillageModelByIdQuery, GetVillageModelByIdResult>
{
    public async Task<GetVillageModelByIdResult> Handle(
        GetVillageModelByIdQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var model = appraisal.VillageModels.FirstOrDefault(m => m.Id == query.ModelId)
                    ?? throw new InvalidOperationException($"Village model {query.ModelId} not found");

        return new GetVillageModelByIdResult(VillageModelMapper.ToDto(model));
    }
}
