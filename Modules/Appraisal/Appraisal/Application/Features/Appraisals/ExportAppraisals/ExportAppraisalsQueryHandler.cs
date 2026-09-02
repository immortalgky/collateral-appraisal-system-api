using Appraisal.Application.Features.Appraisals.Shared;
using System.Text;
using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Appraisal.Application.Features.Shared;
using ClosedXML.Excel;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.ExportAppraisals;

/// <summary>
/// Handles export of appraisals to XLSX or CSV.
/// Applies the same filters as the list query but returns ALL matching rows (up to MaxExportRows).
/// </summary>
public class ExportAppraisalsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUser,
    IAddressNameSearch addressNameSearch
) : IQueryHandler<ExportAppraisalsQuery, ExportAppraisalsResult>
{
    private const int MaxExportRows = 10_000;

    // Same suppression the list handler carries, for the same reason — this handler started
    // interpolating ViewFrom when the free-text search moved to a front-joined derived table, which
    // is what put it in front of the rule.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube",
        "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "Nothing user-supplied is interpolated. MaxExportRows is a const; ViewFrom and " +
            "WhereClause are assembled by AppraisalFilterBuilder from string literals with every " +
            "value bound as a @parameter and passed as filter.Parameters; orderBy comes from " +
            "BuildOrderBy, which can only emit a column drawn from the AllowedSortFields set plus " +
            "ASC/DESC plus the literal Id tiebreaker — a caller's sortBy that is not in that set " +
            "is replaced by CreatedAt, never echoed; SearchQueryHint is one of two literals. See " +
            "AppraisalFilterBuilderTests for the pinned output.")]
    public async Task<ExportAppraisalsResult> Handle(
        ExportAppraisalsQuery query,
        CancellationToken cancellationToken)
    {
        var enforcedCompanyId = AppraisalAccessScope.GetEnforcedCompanyId(currentUser);
        // RequiresView is ignored on purpose: the export always reads the view, so it never
        // takes the cheap base-table path. Check the flag before pointing any query at
        // appraisal.Appraisals — the filter may reference columns only the view has.
        // Same resolution as the list, so an export of a search reproduces exactly what was on screen.
        var addressMatch = await addressNameSearch.MatchAsync(query.Filter?.Search, cancellationToken);
        var filter =
            AppraisalFilterBuilder.BuildFilter(query.Filter, enforcedCompanyId, addressMatch: addressMatch);
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(query.Filter);

        // ViewFrom, not the view name: a free-text search joins in front of the view and needs
        // FORCE ORDER to stay there. This handler builds its own statement, so it appends the hint
        // itself rather than going through DapperPaginationExtensions.
        var sql = $"SELECT TOP({MaxExportRows}) v.* FROM {filter.ViewFrom}{filter.WhereClause}"
                  + $" ORDER BY {orderBy}{filter.SearchQueryHint}";

        using var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<AppraisalDto>(sql, filter.Parameters);
        var rowList = rows.ToList();

        byte[] fileBytes;
        string contentType;
        string fileName;
        var timestamp = dateTimeProvider.ApplicationNow.ToString("yyyyMMdd-HHmmss");

        if (string.Equals(query.Format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            fileBytes = GenerateCsv(rowList);
            contentType = "text/csv";
            fileName = $"appraisals-{timestamp}.csv";
        }
        else
        {
            fileBytes = GenerateExcel(rowList);
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            fileName = $"appraisals-{timestamp}.xlsx";
        }

        return new ExportAppraisalsResult(fileBytes, contentType, fileName);
    }

    private static byte[] GenerateExcel(List<AppraisalDto> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Appraisals");

        // Headers
        var headers = new[]
        {
            "Appraisal Number", "Request Number", "Customer", "Status", "Type", "Priority",
            "Province", "District", "SLA Status", "SLA Due Date", "Assignment Type",
            "Company", "Created At", "Facility Limit", "Property Count"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // Style header row
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data rows
        var row = 2;
        foreach (var item in rows)
        {
            ws.Cell(row, 1).Value = item.AppraisalNumber ?? "";
            ws.Cell(row, 2).Value = item.RequestNumber ?? "";
            ws.Cell(row, 3).Value = item.CustomerName ?? "";
            ws.Cell(row, 4).Value = item.Status;
            ws.Cell(row, 5).Value = item.AppraisalType;
            ws.Cell(row, 6).Value = item.Priority;
            ws.Cell(row, 7).Value = item.Province ?? "";
            ws.Cell(row, 8).Value = item.District ?? "";
            ws.Cell(row, 9).Value = item.SLAStatus ?? "";
            ws.Cell(row, 10).SetValue(item.SLADueDate);
            ws.Cell(row, 11).Value = item.AssignmentType ?? "";
            ws.Cell(row, 12).Value = CompanyLabel(item) ?? "";
            ws.Cell(row, 13).SetValue(item.CreatedAt);
            ws.Cell(row, 14).SetValue(item.FacilityLimit ?? 0);
            ws.Cell(row, 15).Value = item.PropertyCount;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] GenerateCsv(List<AppraisalDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Appraisal Number,Request Number,Customer,Status,Type,Priority,Province,District,SLA Status,SLA Due Date,Assignment Type,Company,Created At,Facility Limit,Property Count");

        foreach (var item in rows)
        {
            sb.AppendLine(
                $"\"{Esc(item.AppraisalNumber)}\",\"{Esc(item.RequestNumber)}\",\"{Esc(item.CustomerName)}\"," +
                $"\"{item.Status}\",\"{item.AppraisalType}\",\"{item.Priority}\"," +
                $"\"{Esc(item.Province)}\",\"{Esc(item.District)}\",\"{item.SLAStatus}\"," +
                $"\"{item.SLADueDate:yyyy-MM-dd}\",\"{item.AssignmentType}\"," +
                $"\"{Esc(CompanyLabel(item))}\",\"{item.CreatedAt:yyyy-MM-dd HH:mm}\"," +
                $"{item.FacilityLimit ?? 0},{item.PropertyCount}");
        }

        // UTF-8 BOM ensures Excel opens the file with correct encoding
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Esc(string? value) => (value ?? "").Replace("\"", "\"\"");

    /// <summary>
    /// Thai-first company label. The export has no request locale to pick from, so it follows the
    /// same convention as the generated reports: prefer the Thai name, fall back to English.
    /// </summary>
    private static string? CompanyLabel(AppraisalDto item) =>
        string.IsNullOrWhiteSpace(item.CompanyNameLocal) ? item.CompanyName : item.CompanyNameLocal;
}
