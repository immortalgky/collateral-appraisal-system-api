namespace Appraisal.Application.Features.Appraisals.GetPhotoTopics;

public record GetPhotoTopicsResult(List<PhotoTopicDto> Topics);

public record PhotoTopicDto(
    Guid Id,
    string TopicName,
    int SortOrder,
    int DisplayColumns,
    int PhotoCount,
    List<TopicPhotoDto> Photos
);

public record TopicPhotoDto(
    Guid Id,
    Guid DocumentId,
    int PhotoNumber,
    string? Caption
);
