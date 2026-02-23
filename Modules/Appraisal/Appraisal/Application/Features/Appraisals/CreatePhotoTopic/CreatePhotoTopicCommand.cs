using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appraisals.CreatePhotoTopic;

public record CreatePhotoTopicCommand(
    Guid AppraisalId,
    string TopicName,
    int SortOrder,
    int DisplayColumns = 1
) : ICommand<CreatePhotoTopicResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
