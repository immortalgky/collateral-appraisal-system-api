using Shared.CQRS;

namespace Common.Application.Features.Search.GlobalSearch;

public record GlobalSearchQuery(string Q, string Filter = "all", int Limit = 5) : IQuery<GlobalSearchResult>;
