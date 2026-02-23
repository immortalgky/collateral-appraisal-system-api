using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appraisals.AssignPhotoToTopic;

public record AssignPhotoToTopicCommand(
    Guid PhotoId,
    List<Guid> PhotoTopicIds
) : ICommand<AssignPhotoToTopicResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
