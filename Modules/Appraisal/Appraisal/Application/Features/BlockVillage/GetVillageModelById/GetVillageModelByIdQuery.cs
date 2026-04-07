namespace Appraisal.Application.Features.BlockVillage.GetVillageModelById;

public record GetVillageModelByIdQuery(Guid AppraisalId, Guid ModelId) : IQuery<GetVillageModelByIdResult>;
