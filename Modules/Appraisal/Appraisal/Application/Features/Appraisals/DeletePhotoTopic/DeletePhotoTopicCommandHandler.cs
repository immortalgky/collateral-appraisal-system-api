namespace Appraisal.Application.Features.Appraisals.DeletePhotoTopic;

public class DeletePhotoTopicCommandHandler(
    IPhotoTopicRepository photoTopicRepository
) : ICommandHandler<DeletePhotoTopicCommand, DeletePhotoTopicResult>
{
    public async Task<DeletePhotoTopicResult> Handle(
        DeletePhotoTopicCommand command,
        CancellationToken cancellationToken)
    {
        var topic = await photoTopicRepository.GetByIdAsync(command.TopicId, cancellationToken);

        if (topic is null)
            throw new InvalidOperationException($"Photo topic with ID {command.TopicId} not found");

        var hasPhotos = await photoTopicRepository.HasPhotosAsync(command.TopicId, cancellationToken);

        if (hasPhotos)
            throw new BadRequestException("Cannot delete a topic that still has photos assigned to it. Remove or reassign all photos first.");

        await photoTopicRepository.DeleteAsync(topic, cancellationToken);

        return new DeletePhotoTopicResult(true);
    }
}
