using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appraisals.UpdatePhotoTopic;

public record UpdatePhotoTopicCommand(
    Guid TopicId,
    string TopicName,
    int SortOrder,
    int DisplayColumns
) : ICommand<UpdatePhotoTopicResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
