namespace Appraisal.Application.Features.BlockVillage.GetVillageModels;

public class GetVillageModelsQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetVillageModelsQuery, GetVillageModelsResult>
{
    public async Task<GetVillageModelsResult> Handle(
        GetVillageModelsQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var models = appraisal.VillageModels.Select(VillageModelMapper.ToDto).ToList();

        return new GetVillageModelsResult(models);
    }
}
