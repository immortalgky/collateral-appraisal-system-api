namespace Common.Application.Features.SavedSearches.CreateSavedSearch;

public record CreateSavedSearchRequest(
    string Name,
    string EntityType,
    string FiltersJson,
    string? SortBy,
    string? SortDir);
