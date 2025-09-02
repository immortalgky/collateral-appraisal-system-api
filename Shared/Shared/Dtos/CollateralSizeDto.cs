namespace Shared.Dtos;

public record CollateralSizeDto(
    string? Capacity,
    decimal? Width,
    decimal? Length,
    decimal? Height
);
