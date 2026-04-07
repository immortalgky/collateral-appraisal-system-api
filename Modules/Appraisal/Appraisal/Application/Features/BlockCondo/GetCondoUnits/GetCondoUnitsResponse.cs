namespace Appraisal.Application.Features.BlockCondo.GetCondoUnits;

public record GetCondoUnitsResponse(
    IReadOnlyList<CondoUnitDto> Units,
    IReadOnlyList<string> Towers,
    IReadOnlyList<string> Models,
    int TotalUnits
);
