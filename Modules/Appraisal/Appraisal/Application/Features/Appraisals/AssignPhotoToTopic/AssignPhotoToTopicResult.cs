namespace Appraisal.Application.Features.Appraisals.AssignPhotoToTopic;

public record AssignPhotoToTopicResult(Guid PhotoId, List<Guid> PhotoTopicIds);
