using System.Diagnostics;
using Dapper;
using Reporting.Contracts;
using Shared.Data;
using Shared.Pagination;
using Shared.Time;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>
/// Executes any <see cref="ReportDefinition{TRow,TFilter}"/> as a paginated preview or a full export.
/// One implementation serves all 12 operational reports.
/// </summary>
public interface IOperationalReportRunner
{
    Task<PaginatedResult<TRow>> PreviewAsync<TRow, TFilter>(
        ReportDefinition<TRow, TFilter> definition, TFilter filter, PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<ReportFile> ExportAsync<TRow, TFilter>(
        ReportDefinition<TRow, TFilter> definition, TFilter filter, ReportFormat format,
        CancellationToken cancellationToken = default);
}

internal sealed class OperationalReportRunner(
    ISqlConnectionFactory connectionFactory,
    ITabularExporter exporter,
    IReportFilterResolver filterResolver,
    IReportAuditLogger auditLogger,
    IDateTimeProvider dateTimeProvider) : IOperationalReportRunner
{
    public async Task<PaginatedResult<TRow>> PreviewAsync<TRow, TFilter>(
        ReportDefinition<TRow, TFilter> definition, TFilter filter, PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = definition.Build(filter);
        var result = await connectionFactory.QueryPaginatedAsync<TRow>(
            sql, definition.OrderBy(filter), pagination, parameters);

        if (definition.EnrichAsync is not null)
            await definition.EnrichAsync(result.Items.ToList(), cancellationToken);

        return result;
    }

    public async Task<ReportFile> ExportAsync<TRow, TFilter>(
        ReportDefinition<TRow, TFilter> definition, TFilter filter, ReportFormat format,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = dateTimeProvider.ApplicationNow;
        var stopwatch = Stopwatch.StartNew();
        var rowCount = 0;
        ReportFile? file = null;
        string? error = null;

        try
        {
            var (sql, parameters) = definition.Build(filter);
            var pagedSql = $"{sql} ORDER BY {definition.OrderBy(filter)} " +
                           $"OFFSET 0 ROWS FETCH NEXT {definition.MaxRows} ROWS ONLY";

            var connection = connectionFactory.GetOpenConnection();
            var rows = (await connection.QueryAsync<TRow>(pagedSql, parameters)).ToList();
            rowCount = rows.Count;

            if (definition.EnrichAsync is not null)
                await definition.EnrichAsync(rows, cancellationToken);

            // Resolve the applied-filter block (null => report declares no describer => no block).
            IReadOnlyList<FilterCriterion>? appliedFilters = definition.DescribeFilter is null
                ? null
                : await filterResolver.ResolveAsync(definition.DescribeFilter(filter), cancellationToken);

            file = await exporter.ExportAsync(
                rows, definition.Columns, definition.BaseName, format, definition.Title,
                appliedFilters, cancellationToken);
            return file;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await auditLogger.LogExportAsync(
                definition.BaseName, format.ToString(), generatedAt, (int)stopwatch.ElapsedMilliseconds,
                rowCount, file?.Bytes.LongLength, error is null, error, cancellationToken);
        }
    }
}
