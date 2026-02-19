using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appraisals.AssignPhotoToTopic;

public record AssignPhotoToTopicCommand(
    Guid PhotoId,
    Guid? PhotoTopicId
) : ICommand<AssignPhotoToTopicResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
