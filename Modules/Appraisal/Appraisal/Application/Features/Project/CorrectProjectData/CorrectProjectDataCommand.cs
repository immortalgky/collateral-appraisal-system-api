using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;

namespace Appraisal.Application.Features.Project.CorrectProjectData;

public record CorrectProjectDataCommand(
    Guid AppraisalId,
    string Target,
    Guid? TargetId,
    string Reason,
    JsonElement? Data,
    ProjectCorrectionImport? Import = null
) : ICommand<CorrectPropertyDataResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record ProjectCorrectionImport(string FileName, byte[] FileContent);
