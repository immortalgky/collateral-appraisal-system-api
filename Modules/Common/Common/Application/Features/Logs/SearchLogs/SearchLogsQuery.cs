using Shared.CQRS;

namespace Common.Application.Features.Logs.SearchLogs;

public record SearchLogsQuery(SearchLogsFilter Filter) : IQuery<SearchLogsResult>;
