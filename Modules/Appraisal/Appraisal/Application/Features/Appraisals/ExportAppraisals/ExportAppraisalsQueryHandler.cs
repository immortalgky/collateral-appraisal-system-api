using System.Text;
using Appraisal.Application.Features.Appraisals.GetAppraisals;
using ClosedXML.Excel;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Appraisal.Application.Features.Appraisals.ExportAppraisals;

/// <summary>
/// Handles export of appraisals to XLSX or CSV.
/// Applies the same filters as the list query but returns ALL matching rows (up to MaxExportRows).
/// </summary>
public class ExportAppraisalsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<ExportAppraisalsQuery, ExportAppraisalsResult>
{
    private const int MaxExportRows = 10_000;

    public async Task<ExportAppraisalsResult> Handle(
        ExportAppraisalsQuery query,
        CancellationToken cancellationToken)
    {
        var (whereClause, parameters) = AppraisalFilterBuilder.BuildFilter(query.Filter);
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(query.Filter);

        var sql = $"SELECT TOP({MaxExportRows}) * FROM appraisal.vw_AppraisalList{whereClause} ORDER BY {orderBy}";

        using var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<AppraisalDto>(sql, parameters);
        var rowList = rows.ToList();

        byte[] fileBytes;
        string contentType;
        string fileName;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

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
            ws.Cell(row, 12).Value = item.CompanyName ?? "";
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
                $"\"{Esc(item.CompanyName)}\",\"{item.CreatedAt:yyyy-MM-dd HH:mm}\"," +
                $"{item.FacilityLimit ?? 0},{item.PropertyCount}");
        }

        // UTF-8 BOM ensures Excel opens the file with correct encoding
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Esc(string? value) => (value ?? "").Replace("\"", "\"\"");
}
