using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.MarkPhotoForReport;

public record MarkPhotoForReportCommand(
    Guid PhotoId,
    string ReportSection
) : ICommand<MarkPhotoForReportResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
