using Shared.CQRS;

namespace Common.Application.Features.Logs.GetLogSummary;

public record GetLogSummaryQuery(GetLogSummaryFilter Filter) : IQuery<LogSummaryDto>;
