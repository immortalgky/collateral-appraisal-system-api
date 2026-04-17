using Shared.CQRS;

namespace Common.Application.Features.SavedSearches.DeleteSavedSearch;

public record DeleteSavedSearchCommand(Guid Id) : ICommand<bool>;
