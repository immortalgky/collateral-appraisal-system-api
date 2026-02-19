namespace Appraisal.Application.Features.Appraisals.UpdatePhotoTopic;

public class UpdatePhotoTopicCommandHandler(
    IPhotoTopicRepository photoTopicRepository
) : ICommandHandler<UpdatePhotoTopicCommand, UpdatePhotoTopicResult>
{
    public async Task<UpdatePhotoTopicResult> Handle(
        UpdatePhotoTopicCommand command,
        CancellationToken cancellationToken)
    {
        var topic = await photoTopicRepository.GetByIdAsync(command.TopicId, cancellationToken);

        if (topic is null)
            throw new InvalidOperationException($"Photo topic with ID {command.TopicId} not found");

        topic.Update(command.TopicName, command.SortOrder, command.DisplayColumns);

        await photoTopicRepository.UpdateAsync(topic, cancellationToken);

        return new UpdatePhotoTopicResult(topic.Id);
    }
}
