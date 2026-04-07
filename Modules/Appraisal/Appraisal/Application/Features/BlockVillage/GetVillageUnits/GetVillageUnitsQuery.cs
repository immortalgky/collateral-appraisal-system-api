namespace Appraisal.Application.Features.BlockVillage.GetVillageUnits;

public record GetVillageUnitsQuery(Guid AppraisalId) : IQuery<GetVillageUnitsResult>;
