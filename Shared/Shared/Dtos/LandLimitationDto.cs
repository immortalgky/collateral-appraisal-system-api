namespace Shared.Dtos;

public record LandLimitationDto(
    ExpropriationDto Expropriation,
    EncroachmentDto Encroachment,
    string? Electricity,
    decimal? ElectricityDistance,
    bool? IsLandlocked,
    string? IsLandlockedRemark,
    ForestBoundaryDto ForestBoundary,
    string? LimitationOther
);
