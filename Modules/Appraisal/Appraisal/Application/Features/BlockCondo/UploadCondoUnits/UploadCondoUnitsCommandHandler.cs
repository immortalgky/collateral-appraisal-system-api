using Appraisal.Application.Configurations;
using ClosedXML.Excel;

namespace Appraisal.Application.Features.BlockCondo.UploadCondoUnits;

public class UploadCondoUnitsCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<UploadCondoUnitsCommand, UploadCondoUnitsResult>
{
    public async Task<UploadCondoUnitsResult> Handle(
        UploadCondoUnitsCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var units = ParseExcelFile(command.FileStream, command.AppraisalId);

        const int maxUnits = 10_000;
        if (units.Count > maxUnits)
            throw new BadRequestException(
                $"Too many units. Maximum allowed is {maxUnits}, but the file contains {units.Count}.");

        var upload = appraisal.ImportCondoUnits(command.FileName, command.DocumentId, units);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UploadCondoUnitsResult(upload.Id, units.Count);
    }

    /// <summary>
    /// Parses the Excel file into CondoUnit entities.
    /// Expected columns: Floor, TowerName, CondoRegistrationNumber, RoomNumber, ModelType, UsableArea, SellingPrice
    /// </summary>
    private static List<CondoUnit> ParseExcelFile(Stream stream, Guid appraisalId)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var units = new List<CondoUnit>();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++) // Skip header row
        {
            var floorText = worksheet.Cell(row, 1).GetString().Trim();
            var towerName = worksheet.Cell(row, 2).GetString().Trim();
            var condoRegNumber = worksheet.Cell(row, 3).GetString().Trim();
            var roomNumber = worksheet.Cell(row, 4).GetString().Trim();
            var modelType = worksheet.Cell(row, 5).GetString().Trim();
            var usableAreaText = worksheet.Cell(row, 6).GetString().Trim();
            var sellingPriceText = worksheet.Cell(row, 7).GetString().Trim();

            // Skip empty rows
            if (string.IsNullOrWhiteSpace(floorText) && string.IsNullOrWhiteSpace(roomNumber))
                continue;

            int.TryParse(floorText, out var floor);
            decimal.TryParse(usableAreaText, out var usableArea);
            decimal.TryParse(sellingPriceText, out var sellingPrice);

            var unit = CondoUnit.Create(
                appraisalId: appraisalId,
                uploadBatchId: Guid.Empty, // Placeholder — domain's ImportCondoUnits sets the real value
                sequenceNumber: units.Count + 1,
                floor: floor != 0 ? floor : null,
                towerName: string.IsNullOrEmpty(towerName) ? null : towerName,
                condoRegistrationNumber: string.IsNullOrEmpty(condoRegNumber) ? null : condoRegNumber,
                roomNumber: string.IsNullOrEmpty(roomNumber) ? null : roomNumber,
                modelType: string.IsNullOrEmpty(modelType) ? null : modelType,
                usableArea: usableArea != 0 ? usableArea : null,
                sellingPrice: sellingPrice != 0 ? sellingPrice : null);

            units.Add(unit);
        }

        return units;
    }
}
