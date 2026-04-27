namespace Appraisal.Application.Features.Project.UploadProjectUnits;

/// <summary>Command to upload project units from an Excel file.</summary>
public record UploadProjectUnitsCommand(
    Guid AppraisalId,
    string FileName,
    Guid? DocumentId,
    Stream FileStream
) : ICommand<UploadProjectUnitsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
