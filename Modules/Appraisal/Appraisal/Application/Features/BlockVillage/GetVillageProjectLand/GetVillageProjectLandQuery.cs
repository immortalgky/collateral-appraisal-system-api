namespace Appraisal.Application.Features.BlockVillage.GetVillageProjectLand;

public record GetVillageProjectLandQuery(Guid AppraisalId) : IQuery<GetVillageProjectLandResult>;
