using System.Diagnostics;
using Auth.Contracts.Users;
using Dapper;
using Reporting.Contracts;
using Shared.Data;
using Shared.Identity;
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
    ICurrentUserService currentUser,
    IUserLookupService userLookup,
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

        // Post-enrich filter (e.g. SLA > 2 days) — preview filters the current page only, so the
        // total count still reflects the pre-filter SQL count. The export path filters the full set.
        if (definition.PostEnrichFilter is not null)
        {
            var filtered = result.Items.Where(definition.PostEnrichFilter).ToList();
            result = new PaginatedResult<TRow>(filtered, result.Count, result.PageNumber, result.PageSize);
        }

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

            if (definition.PostEnrichFilter is not null)
                rows = rows.Where(definition.PostEnrichFilter).ToList();
            rowCount = rows.Count;

            // Resolve the applied-filter block (null => report declares no describer => no block).
            IReadOnlyList<FilterCriterion>? appliedFilters = definition.DescribeFilter is null
                ? null
                : await filterResolver.ResolveAsync(definition.DescribeFilter(filter), cancellationToken);

            var signoffs = definition.IncludeSignoffFooter
                ? await BuildSignoffsAsync(cancellationToken)
                : null;

            file = await exporter.ExportAsync(
                rows, definition.Columns, definition.BaseName, format, definition.Title,
                appliedFilters, signoffs, cancellationToken);
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

    /// <summary>
    /// FSD footer: "Print Report By" (ผู้สรุปรายงาน) defaults to the user running the export;
    /// "Approve Report By" (ผู้ตรวจสอบ) is intentionally blank, to be signed by hand.
    /// Resolution never throws — an unresolved user degrades to a blank line rather than failing
    /// the export. Exports always run on an HTTP request, so UserCode is normally populated.
    /// </summary>
    private async Task<IReadOnlyList<ReportSignoff>> BuildSignoffsAsync(CancellationToken ct)
    {
        var code = currentUser.UserCode;
        string? printedBy = code;

        if (!string.IsNullOrWhiteSpace(code))
        {
            try
            {
                var users = await userLookup.GetByUsernamesAsync([code], ct);
                if (users.TryGetValue(code, out var u))
                {
                    // "P5229 - First Last": the code matches what the export audit log records
                    // (ReportGenerationLog.GeneratedBy), keeping a printed report reconcilable.
                    var name = $"{u.FirstName} {u.LastName}".Trim();
                    if (name.Length > 0) printedBy = $"{code} - {name}";
                }
            }
            catch (Exception)
            {
                // Keep the bare code; a name lookup must never fail an export.
            }
        }

        return [new ReportSignoff("Print Report By", printedBy), new ReportSignoff("Approve Report By", null)];
    }
}
