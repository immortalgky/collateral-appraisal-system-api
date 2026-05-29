using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.SupportingDataMaintenance.AddSupportingDetailImage;

/// <summary>
/// Command to add an image to a supporting detail.
/// Stores DocumentId + StorageUrl directly — no appraisal gallery involved.
/// </summary>
public record AddSupportingDetailImageCommand(
    Guid SupportingId,
    Guid DetailId,
    Guid DocumentId,
    string StorageUrl,
    string? FileName = null,
    string? Title = null,
    string? Description = null
) : ICommand<AddSupportingDetailImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
