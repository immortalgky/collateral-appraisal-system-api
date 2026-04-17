namespace Common.Application.Features.SavedSearches.GetSavedSearches;

/// <summary>
/// Shared projection used in list responses for the Saved Searches feature.
/// </summary>
public sealed record SavedSearchDto(
    Guid Id,
    string Name,
    string EntityType,
    string FiltersJson,
    string? SortBy,
    string? SortDir,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
