using System.Globalization;
using Appraisal.Application.Configurations;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.Uploads;
using ClosedXML.Excel;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.PricingAnalysis.UploadHypothesisUnitDetails;

/// <summary>
/// Parses an Excel file and replaces unit rows on the HypothesisAnalysis.
/// Validates file format per FSD column spec per variant.
/// Deactivates the previously-active upload and activates the new one.
/// Parse errors are collected and returned as a 400 with row/field/value detail.
/// </summary>
public class UploadHypothesisUnitDetailsCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<UploadHypothesisUnitDetailsCommand, UploadHypothesisUnitDetailsResult>
{
    private const int MaxRows = 20_000;

    public async Task<UploadHypothesisUnitDetailsResult> Handle(
        UploadHypothesisUnitDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new InvalidOperationException(
                                 $"PricingAnalysis {command.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new InvalidOperationException(
                         $"PricingAnalysisMethod {command.MethodId} not found");

        var analysis = method.HypothesisAnalysis
                       ?? throw new InvalidOperationException(
                           "Hypothesis analysis not yet generated. Call GenerateHypothesisAnalysis first.");

        var uploadedAt = DateTime.UtcNow;
        HypothesisUnitDetailUpload upload;

        if (analysis.Variant == HypothesisVariant.LandBuilding)
        {
            var rows = ParseLandBuildingExcel(command.FileStream, analysis.Id);
            if (rows.Count > MaxRows)
                throw new BadRequestException(
                    $"Too many rows. Maximum is {MaxRows}, file contains {rows.Count}.");

            upload = analysis.AddUpload(command.FileName, uploadedAt, rows.Count);

            // Assign upload ID to rows
            var rowsWithUpload = rows.Select(r => LandBuildingUnitRow.Create(
                upload.Id, analysis.Id, r.SequenceNumber,
                r.PlanNo, r.HouseNo, r.ModelName, r.Location, r.FloorNo,
                r.LandAreaSqWa, r.UsableAreaSqM, r.SellingPrice,
                r.Remark1, r.Remark2)).ToList();

            analysis.ReplaceLandBuildingRows(rowsWithUpload);
        }
        else
        {
            var rows = ParseCondominiumExcel(command.FileStream, analysis.Id);
            if (rows.Count > MaxRows)
                throw new BadRequestException(
                    $"Too many rows. Maximum is {MaxRows}, file contains {rows.Count}.");

            upload = analysis.AddUpload(command.FileName, uploadedAt, rows.Count);

            var rowsWithUpload = rows.Select(r => CondominiumUnitRow.Create(
                upload.Id, analysis.Id, r.SequenceNumber,
                r.FloorNo, r.Building, r.AptNo, r.Apartment, r.ModelType,
                r.UsableAreaSqM, r.SellingPrice,
                r.Remark1, r.Remark2)).ToList();

            analysis.ReplaceCondominiumRows(rowsWithUpload);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UploadHypothesisUnitDetailsResult(upload.Id, upload.RowCount, upload.IsActive);
    }

    // ── L&B Excel parser ──────────────────────────────────────────────────

    private static List<LandBuildingUnitRow> ParseLandBuildingExcel(Stream stream, Guid analysisId)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();
        var headers = BuildHeaderMap(ws);

        var planCol = Resolve(headers, "Plan No.", "Plan No", "Plan Number", "PlanNo");
        var houseCol = Resolve(headers, "House No.", "House No", "House Number", "HouseNo");
        var modelCol = Resolve(headers, "Model Name", "ModelName", "Model");
        var locationCol = TryResolve(headers, "Location");
        var floorCol = TryResolve(headers, "Floor No.", "Floor No", "FloorNo");
        var landAreaCol = Resolve(headers, "Land Area (Sq.Wa)", "Land Area", "LandArea");
        var usableAreaCol = TryResolve(headers, "Usable Area (Sq.M)", "Usable Area (Sq.M.)", "Usable Area", "UsableArea");
        var sellingPriceCol = Resolve(headers, "Selling Price (Baht)", "Selling Price", "SellingPrice");
        var remark1Col = TryResolve(headers, "Remark 1", "Remark1");
        var remark2Col = TryResolve(headers, "Remark 2", "Remark2");

        var rows = new List<LandBuildingUnitRow>();
        var errors = new List<ParseRowError>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var planNo = ws.Cell(row, planCol).GetString().Trim();
            var houseNo = ws.Cell(row, houseCol).GetString().Trim();
            var modelName = ws.Cell(row, modelCol).GetString().Trim();
            var location = locationCol is { } lc ? ws.Cell(row, lc).GetString().Trim() : string.Empty;
            var remark1 = remark1Col is { } r1c ? ws.Cell(row, r1c).GetString().Trim() : string.Empty;
            var remark2 = remark2Col is { } r2c ? ws.Cell(row, r2c).GetString().Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(planNo) && string.IsNullOrWhiteSpace(houseNo))
                continue;

            int? floorNo = floorCol is { } fc ? TryParseInt(ws.Cell(row, fc), row, "Floor No", errors) : null;
            decimal? landArea = TryParseDecimal(ws.Cell(row, landAreaCol), row, "Land Area (Sq.Wa)", errors);
            decimal? usableArea = usableAreaCol is { } uac
                ? TryParseDecimal(ws.Cell(row, uac), row, "Usable Area (Sq.M)", errors)
                : null;
            decimal? sellingPrice = TryParseDecimal(ws.Cell(row, sellingPriceCol), row, "Selling Price", errors);

            rows.Add(LandBuildingUnitRow.Create(
                uploadId: Guid.Empty, // reassigned after upload is created
                hypothesisAnalysisId: analysisId,
                sequenceNumber: rows.Count + 1,
                planNo: string.IsNullOrEmpty(planNo) ? null : planNo,
                houseNo: string.IsNullOrEmpty(houseNo) ? null : houseNo,
                modelName: string.IsNullOrEmpty(modelName) ? null : modelName,
                location: string.IsNullOrEmpty(location) ? null : location,
                floorNo: floorNo,
                landAreaSqWa: landArea,
                usableAreaSqM: usableArea,
                sellingPrice: sellingPrice,
                remark1: string.IsNullOrEmpty(remark1) ? null : remark1,
                remark2: string.IsNullOrEmpty(remark2) ? null : remark2));
        }

        if (errors.Count > 0)
            throw new BadRequestException(BuildParseErrorMessage(errors));

        return rows;
    }

    // ── Condo Excel parser ────────────────────────────────────────────────

    private static List<CondominiumUnitRow> ParseCondominiumExcel(Stream stream, Guid analysisId)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();
        var headers = BuildHeaderMap(ws);

        var floorCol = Resolve(headers, "Floor No", "Floor No.", "Floor", "FloorNo");
        var buildingCol = Resolve(headers, "Building", "Building Name");
        var aptCol = Resolve(headers, "Apartment No.", "Apartment No", "Apt No", "Room Number", "AptNo", "RoomNo");
        var apartmentCol = TryResolve(headers, "Apartment");
        var modelCol = Resolve(headers, "Apartment Type", "Model Type", "ModelType");
        var usableAreaCol = Resolve(headers, "Condo Area (Sq.M)", "Condo Area", "Usable Area (Sq.M.)", "Usable Area", "UsableArea");
        var sellingPriceCol = Resolve(headers, "Selling Price (Baht)", "Selling Price", "SellingPrice");
        var remark1Col = TryResolve(headers, "Remark 1", "Remark1");
        var remark2Col = TryResolve(headers, "Remark 2", "Remark2");

        var rows = new List<CondominiumUnitRow>();
        var errors = new List<ParseRowError>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var building = ws.Cell(row, buildingCol).GetString().Trim();
            var aptNo = ws.Cell(row, aptCol).GetString().Trim();
            var apartment = apartmentCol is { } apc ? ws.Cell(row, apc).GetString().Trim() : string.Empty;
            var modelType = ws.Cell(row, modelCol).GetString().Trim();
            var remark1 = remark1Col is { } r1c ? ws.Cell(row, r1c).GetString().Trim() : string.Empty;
            var remark2 = remark2Col is { } r2c ? ws.Cell(row, r2c).GetString().Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(aptNo) && string.IsNullOrWhiteSpace(building))
                continue;

            int? floor = TryParseInt(ws.Cell(row, floorCol), row, "Floor No", errors);
            decimal? usableArea = TryParseDecimal(ws.Cell(row, usableAreaCol), row, "Condo Area (Sq.M)", errors);
            decimal? sellingPrice = TryParseDecimal(ws.Cell(row, sellingPriceCol), row, "Selling Price", errors);

            rows.Add(CondominiumUnitRow.Create(
                uploadId: Guid.Empty,
                hypothesisAnalysisId: analysisId,
                sequenceNumber: rows.Count + 1,
                floorNo: floor,
                building: string.IsNullOrEmpty(building) ? null : building,
                aptNo: string.IsNullOrEmpty(aptNo) ? null : aptNo,
                apartment: string.IsNullOrEmpty(apartment) ? null : apartment,
                modelType: string.IsNullOrEmpty(modelType) ? null : modelType,
                usableAreaSqM: usableArea,
                sellingPrice: sellingPrice,
                remark1: string.IsNullOrEmpty(remark1) ? null : remark1,
                remark2: string.IsNullOrEmpty(remark2) ? null : remark2));
        }

        if (errors.Count > 0)
            throw new BadRequestException(BuildParseErrorMessage(errors));

        return rows;
    }

    // ── Parse helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Tries to read a decimal from a cell using ClosedXML typed accessor first,
    /// then falls back to InvariantCulture string parse.
    /// Returns null for genuinely blank cells. Records an error for non-blank non-parseable values.
    /// </summary>
    private static decimal? TryParseDecimal(IXLCell cell, int rowNumber, string fieldName, List<ParseRowError> errors)
    {
        if (cell.IsEmpty())
            return null;

        // Prefer ClosedXML typed accessor (respects numeric cell format regardless of locale)
        try
        {
            return cell.GetValue<decimal>();
        }
        catch
        {
            // Fall back: try invariant-culture string parse
            var raw = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            errors.Add(new ParseRowError(rowNumber, fieldName, raw, "not a number"));
            return null;
        }
    }

    /// <summary>
    /// Tries to read an int from a cell. Returns null for blank cells.
    /// Records an error for non-blank non-parseable values.
    /// </summary>
    private static int? TryParseInt(IXLCell cell, int rowNumber, string fieldName, List<ParseRowError> errors)
    {
        if (cell.IsEmpty())
            return null;

        try
        {
            return cell.GetValue<int>();
        }
        catch
        {
            var raw = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            if (int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            errors.Add(new ParseRowError(rowNumber, fieldName, raw, "not an integer"));
            return null;
        }
    }

    private static string BuildParseErrorMessage(IReadOnlyList<ParseRowError> errors)
    {
        var lines = errors.Select(e =>
            $"Row {e.Row}, Field '{e.Field}': value '{e.Value}' is {e.Reason}.");
        return $"Excel parse errors:\n{string.Join("\n", lines)}";
    }

    private record ParseRowError(int Row, string Field, string Value, string Reason);

    // ── Header helpers (shared pattern from UploadProjectUnits) ───────────

    private static string NormalizeHeader(string raw) =>
        new string((raw ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

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

    private static int Resolve(Dictionary<string, int> headers, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (headers.TryGetValue(NormalizeHeader(alias), out var idx))
                return idx;
        }

        throw new BadRequestException(
            $"Required column '{aliases[0]}' is missing from the upload. " +
            $"Found columns: {string.Join(", ", headers.Keys)}");
    }

    private static int? TryResolve(Dictionary<string, int> headers, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (headers.TryGetValue(NormalizeHeader(alias), out var idx))
                return idx;
        }
        return null;
    }
}
