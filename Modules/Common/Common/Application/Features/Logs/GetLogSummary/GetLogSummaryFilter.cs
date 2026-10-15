namespace Common.Application.Features.Logs.GetLogSummary;

// Same q/from/to semantics as SearchLogsFilter — levels is intentionally not accepted here, the
// summary always reports counts across every level so the level chips can show totals unaffected
// by which levels happen to be selected in the table.
public record GetLogSummaryFilter(string? Q, DateTime? From, DateTime? To);
