using Shared.CQRS;

namespace Common.Application.Features.SavedSearches.GetSavedSearches;

public record GetSavedSearchesQuery(string? EntityType) : IQuery<GetSavedSearchesResponse>;
