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
                r.PlanNo, r.HouseNo, r.ModelName, r.LandAreaSqWa, r.SellingPrice)).ToList();

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
                r.FloorNo, r.Building, r.AptNo, r.ModelType, r.UsableAreaSqM, r.SellingPrice)).ToList();

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

        var planCol = Resolve(headers, "Plan No", "Plan Number", "PlanNo");
        var houseCol = Resolve(headers, "House No", "House Number", "HouseNo");
        var modelCol = Resolve(headers, "Model Name", "ModelName", "Model");
        var landAreaCol = Resolve(headers, "Land Area (Sq.Wa)", "Land Area", "LandArea");
        var sellingPriceCol = Resolve(headers, "Selling Price", "Selling Price (Baht)", "SellingPrice");

        var rows = new List<LandBuildingUnitRow>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var planNo = ws.Cell(row, planCol).GetString().Trim();
            var houseNo = ws.Cell(row, houseCol).GetString().Trim();
            var modelName = ws.Cell(row, modelCol).GetString().Trim();
            var landAreaText = ws.Cell(row, landAreaCol).GetString().Trim();
            var sellingPriceText = ws.Cell(row, sellingPriceCol).GetString().Trim();

            if (string.IsNullOrWhiteSpace(planNo) && string.IsNullOrWhiteSpace(houseNo))
                continue;

            decimal.TryParse(landAreaText, out var landArea);
            decimal.TryParse(sellingPriceText, out var sellingPrice);

            rows.Add(LandBuildingUnitRow.Create(
                uploadId: Guid.Empty, // will be reassigned after upload is created
                hypothesisAnalysisId: analysisId,
                sequenceNumber: rows.Count + 1,
                planNo: string.IsNullOrEmpty(planNo) ? null : planNo,
                houseNo: string.IsNullOrEmpty(houseNo) ? null : houseNo,
                modelName: string.IsNullOrEmpty(modelName) ? null : modelName,
                landAreaSqWa: landArea != 0 ? landArea : null,
                sellingPrice: sellingPrice != 0 ? sellingPrice : null));
        }

        return rows;
    }

    // ── Condo Excel parser ────────────────────────────────────────────────

    private static List<CondominiumUnitRow> ParseCondominiumExcel(Stream stream, Guid analysisId)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();
        var headers = BuildHeaderMap(ws);

        var floorCol = Resolve(headers, "Floor No", "Floor", "FloorNo");
        var buildingCol = Resolve(headers, "Building", "Building Name");
        var aptCol = Resolve(headers, "Apt No", "Room Number", "AptNo", "RoomNo");
        var modelCol = Resolve(headers, "Model Type", "ModelType");
        var usableAreaCol = Resolve(headers, "Usable Area (Sq.M.)", "Usable Area", "UsableArea");
        var sellingPriceCol = Resolve(headers, "Selling Price", "Selling Price (Baht)", "SellingPrice");

        var rows = new List<CondominiumUnitRow>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var floorText = ws.Cell(row, floorCol).GetString().Trim();
            var building = ws.Cell(row, buildingCol).GetString().Trim();
            var aptNo = ws.Cell(row, aptCol).GetString().Trim();
            var modelType = ws.Cell(row, modelCol).GetString().Trim();
            var usableAreaText = ws.Cell(row, usableAreaCol).GetString().Trim();
            var sellingPriceText = ws.Cell(row, sellingPriceCol).GetString().Trim();

            if (string.IsNullOrWhiteSpace(aptNo) && string.IsNullOrWhiteSpace(building))
                continue;

            int.TryParse(floorText, out var floor);
            decimal.TryParse(usableAreaText, out var usableArea);
            decimal.TryParse(sellingPriceText, out var sellingPrice);

            rows.Add(CondominiumUnitRow.Create(
                uploadId: Guid.Empty,
                hypothesisAnalysisId: analysisId,
                sequenceNumber: rows.Count + 1,
                floorNo: floor != 0 ? floor : null,
                building: string.IsNullOrEmpty(building) ? null : building,
                aptNo: string.IsNullOrEmpty(aptNo) ? null : aptNo,
                modelType: string.IsNullOrEmpty(modelType) ? null : modelType,
                usableAreaSqM: usableArea != 0 ? usableArea : null,
                sellingPrice: sellingPrice != 0 ? sellingPrice : null));
        }

        return rows;
    }

    // ── Header helpers (shared pattern from UploadProjectUnits) ───────────

    private static string NormalizeHeader(string raw) =>
        new string((raw ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

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
}
