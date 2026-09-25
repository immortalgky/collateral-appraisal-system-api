using System.Globalization;
using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles a <see cref="QuotationSummaryModel"/> for the "Quotation Summary" PDF report —
/// a listing of the appraisals bundled into a Request-for-Quotation, mirroring the table shown
/// in the quotation email body (ลำดับ / ApplicationNo / ชื่อลูกค้า / ประเภททรัพย์สิน).
///
/// Data strategy (READ-ONLY Dapper — no EF, no migrations):
///   appraisal.QuotationRequestItems  → line items (ItemNumber, AppraisalNumber, PropertyType code)
///   appraisal.Appraisals             → RequestId (to resolve customer + titles)
///   request.RequestCustomers         → CustomerName(s) per RequestId
///   request.RequestTitles            → per-title detail for the property-description composition
///   parameter.Parameters             → PropertyType / BuildingType / MachineStatus code→Thai description
///
/// entityId = QuotationRequestId (Guid).
/// </summary>
public sealed class QuotationSummaryDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<QuotationSummaryDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "quotation-summary";

    // ── Title-family code sets (mirrors quotationEmailTemplate.ts) ─────────────
    private static readonly Dictionary<string, string> LandBuildingLabels = new()
    {
        ["LB"] = "ที่ดินพร้อมสิ่งปลูกสร้าง",
        ["LS"] = "สิทธิการเช่าที่ดินพร้อมสิ่งปลูกสร้าง",
    };
    private static readonly Dictionary<string, string> LandOnlyLabels = new()
    {
        ["L"] = "ที่ดินเปล่า",
        ["LSL"] = "สิทธการเช่าที่ดิน",
    };
    private static readonly HashSet<string> CondoFamilies = ["U", "LSU"];
    private static readonly HashSet<string> MachineFamilies = ["MAC"];
    private static readonly HashSet<string> LandFamilies = ["L", "LB"];
    private static readonly HashSet<string> LeaseLandFamilies = ["LS", "LSL"];
    private static readonly HashSet<string> BuildingOnlyFamilies = ["B", "LSB"];
    private static readonly HashSet<string> LandOrBuildingFamilies =
        [..LandFamilies, ..LeaseLandFamilies, ..BuildingOnlyFamilies];

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var quotationRequestId))
            throw new NotFoundException("Quotation", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var p = new DynamicParameters();
        p.Add("QuotationRequestId", quotationRequestId);

        // ── 1. items  ────────────────────────────────────
        const string itemsSql = """
            SELECT
                qi.ItemNumber,
                qi.AppraisalNumber,
                qi.PropertyType,
                a.RequestId
            FROM appraisal.QuotationRequestItems qi
            INNER JOIN appraisal.Appraisals a ON a.Id = qi.AppraisalId
            WHERE qi.QuotationRequestId = @QuotationRequestId
            ORDER BY qi.ItemNumber
            """;

        var items = (await connection.QueryAsync<ItemRow>(itemsSql, p)).ToList();
        if (items.Count == 0)
        {
            logger.LogDebug("QuotationSummary: no items for quotation {QuotationRequestId}", quotationRequestId);
            return new QuotationSummaryModel { Rows = Array.Empty<QuotationSummaryRowModel>() };
        }

        var requestIds = items.Select(i => i.RequestId).Distinct().ToArray();

        // ── 2. Customer name(s) per Request ───────────────────────────────────
        const string customersSql = """
            SELECT RequestId, STRING_AGG(Name, ', ') AS CustomerName
            FROM request.RequestCustomers
            WHERE RequestId IN @RequestIds
            GROUP BY RequestId
            """;

        var customerRows = await connection.QueryAsync<CustomerRow>(
            customersSql, new { RequestIds = requestIds });
        var customerNamesByRequest = customerRows.ToDictionary(r => r.RequestId, r => r.CustomerName ?? string.Empty);

        // ── 3. Title detail per RequestId (same shape as GetQuotationByIdQueryHandler.ResolveTitlesAsync) ──
        const string titlesSql = """
            SELECT rt.RequestId,
                   rt.TitleFamily,
                   rt.TitleNumber,
                   rt.BuildingType,
                   rt.AreaRai,
                   rt.AreaNgan,
                   rt.AreaSquareWa,
                   rt.CondoName,
                   rt.RoomNumber,
                   rt.UsableArea,
                   rt.InstallationStatus,
                   rt.NumberOfMachine,
                   dsd.NameTh AS DopaSubDistrictName,
                   dd.NameTh AS DopaDistrictName,
                   dp.NameTh AS DopaProvinceName
            FROM [request].[RequestTitles] rt
            LEFT JOIN [parameter].[DopaSubDistricts] dsd ON dsd.Code = rt.DopaSubDistrict
            LEFT JOIN [parameter].[DopaDistricts] dd ON dd.Code = dsd.DistrictCode
            LEFT JOIN [parameter].[DopaProvinces] dp ON dp.Code = dd.ProvinceCode
            WHERE rt.RequestId IN @RequestIds
            """;

        var titleRows = await connection.QueryAsync<TitleRow>(titlesSql, new { RequestIds = requestIds });
        var titlesByRequest = titleRows
            .GroupBy(r => r.RequestId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── 4. Thai description lookups (PropertyType / BuildingType / MachineStatus) ──
        const string paramsSql = """
            SELECT [Group], Code, Description
            FROM parameter.Parameters
            WHERE [Group] IN ('PropertyType', 'BuildingType', 'MachineStatus')
              AND [Language] = 'TH'
              AND [IsActive] = 1
            """;

        var paramRows = (await connection.QueryAsync<ParameterRow>(paramsSql)).ToList();
        var propertyTypeDescriptions = ToDescriptionMap(paramRows, "PropertyType");
        var buildingTypeDescriptions = ToDescriptionMap(paramRows, "BuildingType");
        var machineStatusDescriptions = ToDescriptionMap(paramRows, "MachineStatus");

        string PropertyTypeDescription(string? code) => Describe(code, propertyTypeDescriptions);
        string BuildingTypeDescription(string? code) => Describe(code, buildingTypeDescriptions);
        string MachineStatusDescription(string? code) => Describe(code, machineStatusDescriptions);

        // ── Build rows ────────────────────────────────────────────────────────
        var rows = items
            .Select((i, idx) =>
            {
                titlesByRequest.TryGetValue(i.RequestId, out var titles);
                var propertyType = BuildPropertyDescription(
                    i.PropertyType,
                    titles ?? [],
                    PropertyTypeDescription,
                    BuildingTypeDescription,
                    MachineStatusDescription);

                return new QuotationSummaryRowModel
                {
                    SeqNo = idx + 1,
                    AppraisalNumber = i.AppraisalNumber,
                    CustomerName = customerNamesByRequest.GetValueOrDefault(i.RequestId, string.Empty),
                    PropertyType = propertyType
                };
            })
            .ToList();

        logger.LogDebug(
            "QuotationSummary model assembled for quotation {QuotationRequestId}: {RowCount} rows",
            quotationRequestId, rows.Count);

        return new QuotationSummaryModel { Rows = rows };
    }

    // ── Property-description composition ──────────

    private static string BuildPropertyDescription(
        string code,
        IReadOnlyList<TitleRow> allTitles,
        Func<string?, string> propertyTypeDescription,
        Func<string?, string> buildingTypeDescription,
        Func<string?, string> machineStatusDescription)
    {
        if (allTitles.Count == 0)
            return propertyTypeDescription(code);

        if (LandOrBuildingFamilies.Contains(code))
        {
            var landTitles = allTitles
                .Where(t => LandFamilies.Contains(t.TitleFamily) || LeaseLandFamilies.Contains(t.TitleFamily))
                .ToList();
            var buildingTitles = allTitles
                .Where(t => BuildingOnlyFamilies.Contains(t.TitleFamily) || t.TitleFamily is "LB" or "LS")
                .ToList();
            var buildingTypes = string.Join(", ", buildingTitles
                .Select(t => buildingTypeDescription(t.BuildingType))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct());

            if (landTitles.Count > 0)
            {
                var isLease = LeaseLandFamilies.Contains(landTitles[0].TitleFamily);
                var label = !string.IsNullOrEmpty(buildingTypes)
                    ? LandBuildingLabels[isLease ? "LS" : "LB"]
                    : LandOnlyLabels[isLease ? "LSL" : "L"];
                var titleNumbers = string.Join(", ", landTitles
                    .Select(t => t.TitleNumber)
                    .Where(s => !string.IsNullOrEmpty(s)));
                var (rai, ngan, wa) = SumThaiLandArea(landTitles);

                return JoinNonEmpty(
                    !string.IsNullOrEmpty(buildingTypes) ? $"{label} ({buildingTypes})" : label,
                    !string.IsNullOrEmpty(titleNumbers) ? $"โฉนดเลขที่ {titleNumbers}" : "",
                    $"เนื้อที่ {FormatWhole(rai)}-{FormatWhole(ngan)}-{FormatArea(wa)}",
                    FormatDopaAddress(landTitles));
            }

            if (buildingTitles.Count > 0)
            {
                return JoinNonEmpty(
                    !string.IsNullOrEmpty(buildingTypes) ? $"สิ่งปลูกสร้าง ({buildingTypes})" : "สิ่งปลูกสร้าง",
                    FormatDopaAddress(buildingTitles));
            }

            return propertyTypeDescription(code);
        }

        var titles = allTitles.Where(t => t.TitleFamily == code).ToList();
        if (titles.Count == 0)
            return propertyTypeDescription(code);

        if (CondoFamilies.Contains(code))
        {
            var roomNumbers = string.Join(", ", titles
                .Select(t => t.RoomNumber)
                .Where(s => !string.IsNullOrEmpty(s)));
            var usableArea = titles.Sum(t => t.UsableArea ?? 0m);

            return JoinNonEmpty(
                !string.IsNullOrEmpty(roomNumbers) ? $"ห้องชุดเลขที่ {roomNumbers}" : "ห้องชุด",
                !string.IsNullOrEmpty(titles[0].CondoName) ? $"โครงการ {titles[0].CondoName}" : "",
                usableArea != 0m ? $"พื้นที่ {FormatArea(usableArea)} ตร.ม" : "",
                FormatDopaAddress(titles));
        }

        if (MachineFamilies.Contains(code))
        {
            var statuses = string.Join(", ", titles
                .Select(t => machineStatusDescription(t.InstallationStatus))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct());
            var totalMachines = titles.Sum(t => t.NumberOfMachine ?? 0);

            return JoinNonEmpty(
                !string.IsNullOrEmpty(statuses) ? $"เครื่องจักร ({statuses})" : "เครื่องจักร",
                totalMachines != 0 ? $"จำนวน {totalMachines} เครื่อง" : "",
                FormatDopaAddress(titles));
        }

        return propertyTypeDescription(code);
    }

    private static (decimal Rai, decimal Ngan, decimal Wa) SumThaiLandArea(IReadOnlyList<TitleRow> titles)
    {
        var totalSqWa = titles.Sum(t => (t.AreaRai ?? 0m) * 400m + (t.AreaNgan ?? 0m) * 100m + (t.AreaSquareWa ?? 0m));
        var rai = Math.Floor(totalSqWa / 400m);
        var afterRai = totalSqWa - rai * 400m;
        var ngan = Math.Floor(afterRai / 100m);
        var wa = Math.Round(afterRai - ngan * 100m, 2, MidpointRounding.AwayFromZero);
        return (rai, ngan, wa);
    }

    private static string FormatDopaAddress(IReadOnlyList<TitleRow> titles)
    {
        var first = titles.Count > 0 ? titles[0] : null;
        if (first is null) return "";

        return JoinNonEmpty(
            !string.IsNullOrEmpty(first.DopaSubDistrictName) ? $"ตำบล {first.DopaSubDistrictName}" : "",
            !string.IsNullOrEmpty(first.DopaDistrictName) ? $"อำเภอ {first.DopaDistrictName}" : "",
            !string.IsNullOrEmpty(first.DopaProvinceName) ? $"จังหวัด {first.DopaProvinceName}" : "");
    }

    private static string JoinNonEmpty(params string[] parts) =>
        string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));

    private static string FormatWhole(decimal d) => d.ToString("0", CultureInfo.InvariantCulture);

    private static string FormatArea(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Describe(string? code, IReadOnlyDictionary<string, string> descriptions)
    {
        if (string.IsNullOrEmpty(code)) return "";
        return descriptions.GetValueOrDefault(code, code);
    }

    private static Dictionary<string, string> ToDescriptionMap(IEnumerable<ParameterRow> rows, string group) =>
        rows.Where(r => r.Group == group).ToDictionary(r => r.Code, r => r.Description ?? r.Code);

    // ── Private Dapper flat DTOs ──────────────────────────────────────────────

    private sealed class ItemRow
    {
        public int ItemNumber { get; init; }
        public string AppraisalNumber { get; init; } = string.Empty;
        public string PropertyType { get; init; } = string.Empty;
        public Guid RequestId { get; init; }
    }

    private sealed class CustomerRow
    {
        public Guid RequestId { get; init; }
        public string? CustomerName { get; init; }
    }

    private sealed class ParameterRow
    {
        public string Group { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string? Description { get; init; }
    }

    private sealed class TitleRow
    {
        public Guid RequestId { get; init; }
        public string TitleFamily { get; init; } = string.Empty;
        public string? TitleNumber { get; init; }
        public string? BuildingType { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
        public string? CondoName { get; init; }
        public string? RoomNumber { get; init; }
        public decimal? UsableArea { get; init; }
        public string? InstallationStatus { get; init; }
        public int? NumberOfMachine { get; init; }
        public string? DopaSubDistrictName { get; init; }
        public string? DopaDistrictName { get; init; }
        public string? DopaProvinceName { get; init; }
    }
}
