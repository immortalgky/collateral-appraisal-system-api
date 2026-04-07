using Appraisal.Application.Configurations;
using ClosedXML.Excel;

namespace Appraisal.Application.Features.BlockVillage.UploadVillageUnits;

public class UploadVillageUnitsCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<UploadVillageUnitsCommand, UploadVillageUnitsResult>
{
    public async Task<UploadVillageUnitsResult> Handle(
        UploadVillageUnitsCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var units = ParseExcelFile(command.FileStream, command.AppraisalId);

        const int maxUnits = 10_000;
        if (units.Count > maxUnits)
            throw new BadRequestException(
                $"Too many units. Maximum allowed is {maxUnits}, but the file contains {units.Count}.");

        var upload = appraisal.ImportVillageUnits(command.FileName, command.DocumentId, units);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UploadVillageUnitsResult(upload.Id, units.Count);
    }

    /// <summary>
    /// Parses the Excel file into VillageUnit entities.
    /// Expected columns: PlotNumber, HouseNumber, ModelName, NumberOfFloors, LandArea, UsableArea, SellingPrice
    /// </summary>
    private static List<VillageUnit> ParseExcelFile(Stream stream, Guid appraisalId)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var units = new List<VillageUnit>();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++) // Skip header row
        {
            var plotNumber = worksheet.Cell(row, 1).GetString().Trim();
            var houseNumber = worksheet.Cell(row, 2).GetString().Trim();
            var modelName = worksheet.Cell(row, 3).GetString().Trim();
            var floorsText = worksheet.Cell(row, 4).GetString().Trim();
            var landAreaText = worksheet.Cell(row, 5).GetString().Trim();
            var usableAreaText = worksheet.Cell(row, 6).GetString().Trim();
            var sellingPriceText = worksheet.Cell(row, 7).GetString().Trim();

            // Skip empty rows
            if (string.IsNullOrWhiteSpace(plotNumber) && string.IsNullOrWhiteSpace(houseNumber))
                continue;

            int.TryParse(floorsText, out var floors);
            decimal.TryParse(landAreaText, out var landArea);
            decimal.TryParse(usableAreaText, out var usableArea);
            decimal.TryParse(sellingPriceText, out var sellingPrice);

            var unit = VillageUnit.Create(
                appraisalId: appraisalId,
                uploadBatchId: Guid.Empty, // Placeholder — aggregate's ImportVillageUnits sets the real value
                sequenceNumber: units.Count + 1,
                plotNumber: string.IsNullOrEmpty(plotNumber) ? null : plotNumber,
                houseNumber: string.IsNullOrEmpty(houseNumber) ? null : houseNumber,
                modelName: string.IsNullOrEmpty(modelName) ? null : modelName,
                numberOfFloors: floors != 0 ? floors : null,
                landArea: landArea != 0 ? landArea : null,
                usableArea: usableArea != 0 ? usableArea : null,
                sellingPrice: sellingPrice != 0 ? sellingPrice : null);

            units.Add(unit);
        }

        return units;
    }
}
