using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.UploadCondoUnits;

public record UploadCondoUnitsCommand(
    Guid AppraisalId,
    string FileName,
    Guid? DocumentId,
    Stream FileStream
) : ICommand<UploadCondoUnitsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
