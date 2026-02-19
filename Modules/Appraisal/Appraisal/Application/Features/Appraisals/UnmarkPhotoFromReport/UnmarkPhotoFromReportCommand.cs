using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnmarkPhotoFromReport;

public record UnmarkPhotoFromReportCommand(
    Guid PhotoId
) : ICommand<UnmarkPhotoFromReportResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
