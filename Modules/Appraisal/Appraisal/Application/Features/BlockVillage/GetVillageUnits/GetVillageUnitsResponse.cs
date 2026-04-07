namespace Appraisal.Application.Features.BlockVillage.GetVillageUnits;

public record GetVillageUnitsResponse(List<VillageUnitDto> Units, List<string> ModelNames, int TotalCount);
