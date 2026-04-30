using ClosedXML.Excel;

namespace Appraisal.Application.Features.Project.UploadProjectUnits;

/// <summary>
/// Handler for uploading project units from an Excel file.
/// Columns are resolved by header name in row 1 (case- and whitespace-insensitive,
/// with a small alias list per logical field) so users can reorder or relabel columns.
/// Branches on ProjectType for the expected header set:
///   Condo:           Floor | TowerName | CondoRegNumber | RoomNumber | ModelType | UsableArea | SellingPrice
///   LandAndBuilding: PlotNumber | HouseNumber | ModelName | NumberOfFloors | LandArea | UsableArea | SellingPrice
/// The aggregate's ImportUnits method handles auto-creating towers/models and linking.
/// </summary>
public class UploadProjectUnitsCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<UploadProjectUnitsCommand, UploadProjectUnitsResult>
{
    private const int MaxUnits = 10_000;

    public async Task<UploadProjectUnitsResult> Handle(
        UploadProjectUnitsCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        var units = project.ProjectType == ProjectType.Condo
            ? ParseCondoExcel(command.FileStream, project.Id)
            : ParseLandAndBuildingExcel(command.FileStream, project.Id);

        if (units.Count > MaxUnits)
            throw new BadRequestException(
                $"Too many units. Maximum allowed is {MaxUnits}, but the file contains {units.Count}.");

        // Each upload replaces the previous unit set (clearing any calculated unit prices via FK
        // cascade). The aggregate also marks earlier uploads as unused.
        var upload = project.ReplaceUnits(command.FileName, command.DocumentId, units);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UploadProjectUnitsResult(upload.Id, units.Count);
    }

    private static List<ProjectUnit> ParseCondoExcel(Stream stream, Guid projectId)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var headers = BuildHeaderMap(worksheet);

        var floorCol = Resolve(headers, "Floor");
        var towerCol = Resolve(headers, "Tower Name", "TowerName", "Tower");
        var condoRegCol = Resolve(headers,
            "Registration Number", "Reg. Number", "Reg Number", "Condo Reg Number", "CondoRegNumber");
        var roomCol = Resolve(headers, "Room Number", "Room No", "RoomNumber");
        var modelCol = Resolve(headers, "Model Type", "ModelType");
        var usableAreaCol = Resolve(headers, "Usable Area", "Usable Area (sq.m.)", "UsableArea");
        var sellingPriceCol = Resolve(headers, "Selling Price", "Selling Price (Baht)", "SellingPrice");

        var units = new List<ProjectUnit>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var floorText = worksheet.Cell(row, floorCol).GetString().Trim();
            var towerName = worksheet.Cell(row, towerCol).GetString().Trim();
            var condoRegNumber = worksheet.Cell(row, condoRegCol).GetString().Trim();
            var roomNumber = worksheet.Cell(row, roomCol).GetString().Trim();
            var modelType = worksheet.Cell(row, modelCol).GetString().Trim();
            var usableAreaText = worksheet.Cell(row, usableAreaCol).GetString().Trim();
            var sellingPriceText = worksheet.Cell(row, sellingPriceCol).GetString().Trim();

            if (string.IsNullOrWhiteSpace(floorText) && string.IsNullOrWhiteSpace(roomNumber))
                continue;

            int.TryParse(floorText, out var floor);
            decimal.TryParse(usableAreaText, out var usableArea);
            decimal.TryParse(sellingPriceText, out var sellingPrice);

            units.Add(ProjectUnit.CreateCondo(
                projectId: projectId,
                uploadBatchId: Guid.Empty, // aggregate ImportUnits sets the real value
                sequenceNumber: units.Count + 1,
                floor: floor != 0 ? floor : null,
                towerName: string.IsNullOrEmpty(towerName) ? null : towerName,
                condoRegistrationNumber: string.IsNullOrEmpty(condoRegNumber) ? null : condoRegNumber,
                roomNumber: string.IsNullOrEmpty(roomNumber) ? null : roomNumber,
                modelType: string.IsNullOrEmpty(modelType) ? null : modelType,
                usableArea: usableArea != 0 ? usableArea : null,
                sellingPrice: sellingPrice != 0 ? sellingPrice : null));
        }

        return units;
    }

    private static List<ProjectUnit> ParseLandAndBuildingExcel(Stream stream, Guid projectId)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var headers = BuildHeaderMap(worksheet);

        var plotCol = Resolve(headers, "Plot Number", "Plot No", "PlotNumber");
        var houseCol = Resolve(headers, "House Number", "House No", "HouseNumber");
        var modelCol = Resolve(headers, "Model Name", "ModelName", "Model");
        var floorsCol = Resolve(headers,
            "Number of Floors", "No. of Floors", "No of Floors", "NumberOfFloors");
        var landAreaCol = Resolve(headers, "Land Area", "Land Area (sq.wa)", "LandArea");
        var usableAreaCol = Resolve(headers, "Usable Area", "Usable Area (sq.m.)", "UsableArea");
        var sellingPriceCol = Resolve(headers, "Selling Price", "Selling Price (Baht)", "SellingPrice");

        var units = new List<ProjectUnit>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var plotNumber = worksheet.Cell(row, plotCol).GetString().Trim();
            var houseNumber = worksheet.Cell(row, houseCol).GetString().Trim();
            var modelName = worksheet.Cell(row, modelCol).GetString().Trim();
            var floorsText = worksheet.Cell(row, floorsCol).GetString().Trim();
            var landAreaText = worksheet.Cell(row, landAreaCol).GetString().Trim();
            var usableAreaText = worksheet.Cell(row, usableAreaCol).GetString().Trim();
            var sellingPriceText = worksheet.Cell(row, sellingPriceCol).GetString().Trim();

            if (string.IsNullOrWhiteSpace(plotNumber) && string.IsNullOrWhiteSpace(houseNumber))
                continue;

            int.TryParse(floorsText, out var floors);
            decimal.TryParse(landAreaText, out var landArea);
            decimal.TryParse(usableAreaText, out var usableArea);
            decimal.TryParse(sellingPriceText, out var sellingPrice);

            units.Add(ProjectUnit.CreateLandAndBuilding(
                projectId: projectId,
                uploadBatchId: Guid.Empty, // aggregate ImportUnits sets the real value
                sequenceNumber: units.Count + 1,
                plotNumber: string.IsNullOrEmpty(plotNumber) ? null : plotNumber,
                houseNumber: string.IsNullOrEmpty(houseNumber) ? null : houseNumber,
                modelType: string.IsNullOrEmpty(modelName) ? null : modelName,
                numberOfFloors: floors != 0 ? floors : null,
                landArea: landArea != 0 ? landArea : null,
                usableArea: usableArea != 0 ? usableArea : null,
                sellingPrice: sellingPrice != 0 ? sellingPrice : null));
        }

        return units;
    }

    // ── Header resolution helpers ─────────────────────────────────────────────

    /// <summary>
    /// Normalize a header cell for case- and whitespace-insensitive matching.
    /// Lowercase + strip all whitespace; punctuation/parens are preserved.
    /// "Usable Area" → "usablearea";  "USABLE AREA" → "usablearea";  " Tower  Name " → "towername".
    /// </summary>
    private static string NormalizeHeader(string raw) =>
        new string((raw ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    /// <summary>
    /// Reads row 1 as headers and returns a normalized-name → 1-based column-index map.
    /// First occurrence wins on duplicate headers; empty header cells are skipped.
    /// </summary>
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

    /// <summary>
    /// Returns the column index for the first matching alias (compared after normalization).
    /// Throws BadRequestException with the canonical (first) alias name if none match.
    /// </summary>
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
