namespace Appraisal.Application.Features.Appraisals.CreatePhotoTopic;

public class CreatePhotoTopicCommandHandler(
    IPhotoTopicRepository photoTopicRepository
) : ICommandHandler<CreatePhotoTopicCommand, CreatePhotoTopicResult>
{
    public async Task<CreatePhotoTopicResult> Handle(
        CreatePhotoTopicCommand command,
        CancellationToken cancellationToken)
    {
        var topic = PhotoTopic.Create(
            command.AppraisalId,
            command.TopicName,
            command.SortOrder,
            command.DisplayColumns);

        await photoTopicRepository.AddAsync(topic, cancellationToken);

        return new CreatePhotoTopicResult(topic.Id);
    }
}
