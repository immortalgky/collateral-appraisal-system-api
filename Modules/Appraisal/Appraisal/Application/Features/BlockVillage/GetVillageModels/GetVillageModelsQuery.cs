namespace Appraisal.Application.Features.BlockVillage.GetVillageModels;

public record GetVillageModelsQuery(Guid AppraisalId) : IQuery<GetVillageModelsResult>;
