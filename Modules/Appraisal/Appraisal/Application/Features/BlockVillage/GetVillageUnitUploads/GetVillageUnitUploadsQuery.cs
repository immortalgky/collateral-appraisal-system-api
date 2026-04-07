namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitUploads;

public record GetVillageUnitUploadsQuery(Guid AppraisalId) : IQuery<GetVillageUnitUploadsResult>;
