namespace Appraisal.Application.Features.BlockVillage.GetVillageProject;

public record GetVillageProjectQuery(Guid AppraisalId) : IQuery<GetVillageProjectResult>;
