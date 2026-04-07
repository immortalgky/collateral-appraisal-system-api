using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.UploadVillageUnits;

public record UploadVillageUnitsCommand(
    Guid AppraisalId,
    string FileName,
    Guid? DocumentId,
    Stream FileStream
) : ICommand<UploadVillageUnitsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
