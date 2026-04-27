using ClosedXML.Excel;

namespace Appraisal.Application.Features.Project.UploadProjectUnits;

/// <summary>
/// Handler for uploading project units from an Excel file.
/// Branches on ProjectType for column layout:
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

        var upload = project.ImportUnits(command.FileName, command.DocumentId, units);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UploadProjectUnitsResult(upload.Id, units.Count);
    }

    private static List<ProjectUnit> ParseCondoExcel(Stream stream, Guid projectId)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var units = new List<ProjectUnit>();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var floorText = worksheet.Cell(row, 1).GetString().Trim();
            var towerName = worksheet.Cell(row, 2).GetString().Trim();
            var condoRegNumber = worksheet.Cell(row, 3).GetString().Trim();
            var roomNumber = worksheet.Cell(row, 4).GetString().Trim();
            var modelType = worksheet.Cell(row, 5).GetString().Trim();
            var usableAreaText = worksheet.Cell(row, 6).GetString().Trim();
            var sellingPriceText = worksheet.Cell(row, 7).GetString().Trim();

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
        var units = new List<ProjectUnit>();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var plotNumber = worksheet.Cell(row, 1).GetString().Trim();
            var houseNumber = worksheet.Cell(row, 2).GetString().Trim();
            var modelName = worksheet.Cell(row, 3).GetString().Trim();
            var floorsText = worksheet.Cell(row, 4).GetString().Trim();
            var landAreaText = worksheet.Cell(row, 5).GetString().Trim();
            var usableAreaText = worksheet.Cell(row, 6).GetString().Trim();
            var sellingPriceText = worksheet.Cell(row, 7).GetString().Trim();

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
}
