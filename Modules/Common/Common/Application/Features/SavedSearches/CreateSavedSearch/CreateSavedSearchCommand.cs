using Shared.CQRS;

namespace Common.Application.Features.SavedSearches.CreateSavedSearch;

public record CreateSavedSearchCommand(
    string Name,
    string EntityType,
    string FiltersJson,
    string? SortBy,
    string? SortDir) : ICommand<CreateSavedSearchResult>;
