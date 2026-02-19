using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appraisals.DeletePhotoTopic;

public record DeletePhotoTopicCommand(
    Guid TopicId
) : ICommand<DeletePhotoTopicResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
