namespace Appraisal.Contracts.Appraisals.Dto;

public record ConsecutiveAreaDto(
    string? NConsecutiveArea,
    decimal? NEstimateLength,
    string? SConsecutiveArea,
    decimal? SEstimateLength,
    string? EConsecutiveArea,
    decimal? EEstimateLength,
    string? WConsecutiveArea,
    decimal? WEstimateLength
);
