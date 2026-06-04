namespace Appraisal.Application.Features.Project.UploadProjectUnits;

/// <summary>
/// Handler for uploading project units from an Excel file.
/// Columns are resolved by header name in row 1 (case- and whitespace-insensitive,
/// with a small alias list per logical field) so users can reorder or relabel columns.
/// Branches on ProjectType for the expected header set:
///   Condo:           Floor | TowerName | CondoRegNumber | RoomNumber | ModelType | UsableArea | SellingPrice
///   LandAndBuilding: PlotNumber | HouseNumber | ModelName | NumberOfFloors | LandArea | UsableArea | SellingPrice
/// The aggregate's ImportUnits method handles auto-creating towers/models and linking.
/// Excel parsing is delegated to <see cref="ProjectUnitExcelParser"/>.
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

        // TODO(Land): Land follows LandAndBuilding Excel format (plot/house columns) in v1
        var units = project.ProjectType == ProjectType.Condo
            ? ProjectUnitExcelParser.ParseCondoExcel(command.FileStream, project.Id)
            : ProjectUnitExcelParser.ParseLandAndBuildingExcel(command.FileStream, project.Id);

        if (units.Count > MaxUnits)
            throw new BadRequestException(
                $"Too many units. Maximum allowed is {MaxUnits}, but the file contains {units.Count}.");

        // Each upload replaces the previous unit set (clearing any calculated unit prices via FK
        // cascade). The aggregate also marks earlier uploads as unused.
        var upload = project.ReplaceUnits(command.FileName, command.DocumentId, units);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UploadProjectUnitsResult(upload.Id, units.Count);
    }
}
