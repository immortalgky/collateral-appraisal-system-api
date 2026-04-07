namespace Appraisal.Application.Features.BlockVillage.GetVillageUnits;

public record GetVillageUnitsResult(List<VillageUnitDto> Units, List<string> ModelNames, int TotalCount);
