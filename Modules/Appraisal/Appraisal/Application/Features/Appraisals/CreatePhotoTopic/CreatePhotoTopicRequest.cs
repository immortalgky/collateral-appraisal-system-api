namespace Appraisal.Application.Features.Appraisals.CreatePhotoTopic;

public record CreatePhotoTopicRequest(
    string TopicName,
    int SortOrder,
    int DisplayColumns = 1
);
