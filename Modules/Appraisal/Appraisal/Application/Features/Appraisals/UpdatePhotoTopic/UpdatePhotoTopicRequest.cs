namespace Appraisal.Application.Features.Appraisals.UpdatePhotoTopic;

public record UpdatePhotoTopicRequest(
    string TopicName,
    int SortOrder,
    int DisplayColumns
);
