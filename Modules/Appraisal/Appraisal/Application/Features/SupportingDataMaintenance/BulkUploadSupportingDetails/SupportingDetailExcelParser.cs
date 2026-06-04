using ClosedXML.Excel;

namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

/// <summary>
/// Parses the bulk-upload Excel file into a list of SupportingDataDetailData records.
///
/// Column layout (row 1 = headers, row 2+ = data):
///   PropertyName | Developer | ModelName | CollateralType* | BuildingType* | LandArea |
///   UsableArea | ProjectName | RoomFloor | HouseNo | SubDistrict | District | Province |
///   Latitude | Longitude | PlotLocationType | PlotLocationTypeOther | PricePerUnit |
///   OfferingPrice | SellingPrice | PhoneNo | InformationDate* | Website | SourceUrl | Remark
///
/// * = required field; all others are optional.
///
/// Validation strategy (all-or-nothing):
///   - Parse every row, collect ALL errors into a List<RowParseError>
///   - If any errors found → throw BulkUploadParseException with the full list
///   - Caller receives a 400 with "rowErrors" in the ProblemDetails extensions
/// </summary>
internal static class SupportingDetailExcelParser
{
    private const int MaxRows = 5_000;

    /// <summary>
    /// Parses the stream and returns parsed rows.
    /// Throws <see cref="BulkUploadParseException"/> if any row fails validation.
    /// Throws <see cref="Shared.Exceptions.BadRequestException"/> if a required header column is missing.
    /// </summary>
    internal static List<SupportingDataDetailData> Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();
        var headers = BuildHeaderMap(ws);

        // Resolve required and optional columns
        // Required columns (Resolve throws BadRequestException if not found)
        var collateralTypeCol = Resolve(headers, "Collateral Type", "CollateralType");
        var buildingTypeCol = Resolve(headers, "Building Type", "BuildingType");
        var infoDateCol = Resolve(headers, "Information Date", "InformationDate", "Info Date");

        // Optional columns (ResolveOptional returns null if not found)
        var propertyNameCol = ResolveOptional(headers, "Property Name", "PropertyName");
        var developerCol = ResolveOptional(headers, "Developer");
        var modelNameCol = ResolveOptional(headers, "Model Name", "ModelName");
        var landAreaCol = ResolveOptional(headers, "Land Area", "LandArea");
        var usableAreaCol = ResolveOptional(headers, "Usable Area", "UsableArea");
        var projectNameCol = ResolveOptional(headers, "Project Name", "ProjectName");
        var roomFloorCol = ResolveOptional(headers, "Room Floor", "RoomFloor", "Floor");
        var houseNoCol = ResolveOptional(headers, "House No", "HouseNo", "House Number");
        var subDistrictCol = ResolveOptional(headers, "Sub District", "SubDistrict");
        var districtCol = ResolveOptional(headers, "District");
        var provinceCol = ResolveOptional(headers, "Province");
        var latitudeCol = ResolveOptional(headers, "Latitude", "Lat");
        var longitudeCol = ResolveOptional(headers, "Longitude", "Lng", "Long");
        var plotLocationTypeCol = ResolveOptional(headers, "Plot Location Type", "PlotLocationType");
        var plotLocationTypeOtherCol = ResolveOptional(headers, "Plot Location Type Other", "PlotLocationTypeOther");
        var pricePerUnitCol = ResolveOptional(headers, "Price Per Unit", "PricePerUnit");
        var offeringPriceCol = ResolveOptional(headers, "Offering Price", "OfferingPrice");
        var sellingPriceCol = ResolveOptional(headers, "Selling Price", "SellingPrice");
        var phoneNoCol = ResolveOptional(headers, "Phone No", "PhoneNo", "Phone");
        var websiteCol = ResolveOptional(headers, "Website");
        var sourceUrlCol = ResolveOptional(headers, "Source URL", "SourceUrl", "Source Url");
        var remarkCol = ResolveOptional(headers, "Remark", "Remarks");

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        if (lastRow - 1 > MaxRows)
            throw new BadRequestException(
                $"Too many rows. Maximum allowed is {MaxRows}, but the file contains {lastRow - 1}.");

        var errors = new List<RowParseError>();
        var results = new List<SupportingDataDetailData>();

        for (var row = 2; row <= lastRow; row++)
        {
            // Skip completely blank rows
            var collateralType = GetString(ws, row, collateralTypeCol);
            var buildingType = GetString(ws, row, buildingTypeCol);
            var infoDateText = GetString(ws, row, infoDateCol);

            // If the three required fields are all empty, treat as a blank row and skip
            if (string.IsNullOrWhiteSpace(collateralType) &&
                string.IsNullOrWhiteSpace(buildingType) &&
                string.IsNullOrWhiteSpace(infoDateText))
                continue;

            // ── Validate required fields ───────────────────────────────────
            if (string.IsNullOrWhiteSpace(collateralType))
                errors.Add(new RowParseError(row, "Collateral Type", "CollateralType is required."));

            if (string.IsNullOrWhiteSpace(buildingType))
                errors.Add(new RowParseError(row, "Building Type", "BuildingType is required."));

            DateTime? infoDate = null;
            if (string.IsNullOrWhiteSpace(infoDateText))
            {
                errors.Add(new RowParseError(row, "Information Date", "InformationDate is required."));
            }
            else
            {
                infoDate = ParseDate(ws, row, infoDateCol, "Information Date", errors);
            }

            // ── Parse optional decimal fields ──────────────────────────────
            var landArea = ParseDecimalOptional(ws, row, landAreaCol, "Land Area", errors);
            var usableArea = ParseDecimalOptional(ws, row, usableAreaCol, "Usable Area", errors);
            var latitude = ParseDecimalOptional(ws, row, latitudeCol, "Latitude", errors);
            var longitude = ParseDecimalOptional(ws, row, longitudeCol, "Longitude", errors);
            var pricePerUnit = ParseDecimalOptional(ws, row, pricePerUnitCol, "Price Per Unit", errors);
            var offeringPrice = ParseDecimalOptional(ws, row, offeringPriceCol, "Offering Price", errors);
            var sellingPrice = ParseDecimalOptional(ws, row, sellingPriceCol, "Selling Price", errors);

            // Only build the record when this row has no errors so far
            // (we still continue looping to collect ALL errors across all rows)
            if (errors.Count == 0 || !errors.Any(e => e.RowNumber == row))
            {
                results.Add(new SupportingDataDetailData(
                    PropertyName: GetStringOrNull(ws, row, propertyNameCol),
                    Developer: GetStringOrNull(ws, row, developerCol),
                    ModelName: GetStringOrNull(ws, row, modelNameCol),
                    CollateralType: collateralType!,
                    BuildingType: buildingType!,
                    LandArea: landArea,
                    UsableArea: usableArea,
                    ProjectName: GetStringOrNull(ws, row, projectNameCol),
                    RoomFloor: GetStringOrNull(ws, row, roomFloorCol),
                    HouseNo: GetStringOrNull(ws, row, houseNoCol),
                    SubDistrict: GetStringOrNull(ws, row, subDistrictCol),
                    District: GetStringOrNull(ws, row, districtCol),
                    Province: GetStringOrNull(ws, row, provinceCol),
                    Latitude: latitude,
                    PlotLocationType: null,
                    PlotLocationTypeOther: null,
                    Longitude: longitude,
                    PricePerUnit: pricePerUnit,
                    OfferingPrice: offeringPrice,
                    SellingPrice: sellingPrice,
                    PhoneNo: GetStringOrNull(ws, row, phoneNoCol),
                    InformationDate: infoDate!.Value,
                    Website: GetStringOrNull(ws, row, websiteCol),
                    SourceUrl: GetStringOrNull(ws, row, sourceUrlCol),
                    Remark: GetStringOrNull(ws, row, remarkCol)
                ));
            }
        }

        // ── All-or-nothing gate ────────────────────────────────────────────
        // If ANY row had an error, throw — nothing gets committed to the DB.
        if (errors.Count > 0)
            throw new BulkUploadParseException(errors.Cast<object>().ToList());

        return results;
    }

    // Date parsing

    /// <summary>
    /// Tries to parse a date cell.  Handles:
    ///   • Excel native date cells (numeric)
    ///   • String "dd/MM/yyyy" or "yyyy-MM-dd"
    ///   • Buddhist calendar years (year > 2400 → subtract 543)
    /// Adds a RowParseError and returns null when it cannot parse.
    /// </summary>
    private static DateTime? ParseDate(
        IXLWorksheet ws, int row, int? col,
        string columnName, List<RowParseError> errors)
    {
        if (col is null) return null;

        var cell = ws.Cell(row, col.Value);

        // Excel stores dates as numbers; ClosedXML can read them directly.
        if (cell.DataType == XLDataType.DateTime)
        {
            var dt = cell.GetDateTime();
            return AdjustBuddhistYear(dt);
        }

        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Try common formats
        if (DateTime.TryParseExact(text, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            return AdjustBuddhistYear(parsed);
        }

        errors.Add(new RowParseError(row, columnName,
            $"'{text}' is not a valid date. Use dd/MM/yyyy format (e.g. 25/12/2024)."));
        return null;
    }

    private static DateTime AdjustBuddhistYear(DateTime dt)
        => dt.Year > 2400 ? dt.AddYears(-543) : dt;

    // Decimal parsing

    private static decimal? ParseDecimalOptional(
        IXLWorksheet ws, int row, int? col,
        string columnName, List<RowParseError> errors)
    {
        if (col is null) return null;

        var cell = ws.Cell(row, col.Value);

        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();

        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (decimal.TryParse(text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
            return value;

        errors.Add(new RowParseError(row, columnName,
            $"'{text}' is not a valid number."));
        return null;
    }

    // String helpers

    private static string GetString(IXLWorksheet ws, int row, int? col)
        => col.HasValue ? ws.Cell(row, col.Value).GetString().Trim() : string.Empty;

    private static string? GetStringOrNull(IXLWorksheet ws, int row, int? col)
    {
        if (col is null) return null;
        var val = ws.Cell(row, col.Value).GetString().Trim();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    // Header resolution helpers

    private static string NormalizeHeader(string raw)
        => new string((raw ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var c = 1; c <= lastCol; c++)
        {
            var key = NormalizeHeader(ws.Cell(1, c).GetString());
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key)) map[key] = c;
        }
        return map;
    }

    /// <summary>Returns the column index; throws BadRequestException if none of the aliases match.</summary>
    private static int Resolve(Dictionary<string, int> headers, params string[] aliases)
    {
        foreach (var alias in aliases)
            if (headers.TryGetValue(NormalizeHeader(alias), out var idx)) return idx;

        throw new BadRequestException(
            $"Required column '{aliases[0]}' is missing. Found: {string.Join(", ", headers.Keys)}");
    }

    /// <summary>Returns the column index, or null if none of the aliases match (column is optional).</summary>
    private static int? ResolveOptional(Dictionary<string, int> headers, params string[] aliases)
    {
        foreach (var alias in aliases)
            if (headers.TryGetValue(NormalizeHeader(alias), out var idx)) return idx;
        return null;
    }
}
