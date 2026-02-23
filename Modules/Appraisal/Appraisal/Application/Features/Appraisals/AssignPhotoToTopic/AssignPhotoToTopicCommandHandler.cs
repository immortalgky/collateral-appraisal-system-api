using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.Appraisals.AssignPhotoToTopic;

public class AssignPhotoToTopicCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<AssignPhotoToTopicCommand, AssignPhotoToTopicResult>
{
    public async Task<AssignPhotoToTopicResult> Handle(
        AssignPhotoToTopicCommand command,
        CancellationToken cancellationToken)
    {
        var photo = await galleryRepository.GetByIdAsync(command.PhotoId, cancellationToken);

        if (photo is null)
            throw new InvalidOperationException($"Gallery photo with ID {command.PhotoId} not found");

        // Get existing topic mappings
        var existingMappings = (await galleryRepository
            .GetTopicMappingsByPhotoIdAsync(command.PhotoId, cancellationToken)).ToList();

        var existingTopicIds = existingMappings.Select(m => m.PhotoTopicId).ToHashSet();
        var desiredTopicIds = command.PhotoTopicIds.ToHashSet();

        // Remove mappings no longer desired
        var toRemove = existingMappings.Where(m => !desiredTopicIds.Contains(m.PhotoTopicId));
        foreach (var mapping in toRemove)
        {
            await galleryRepository.DeleteTopicMappingAsync(mapping, cancellationToken);
        }

        // Add new mappings
        var toAdd = desiredTopicIds.Except(existingTopicIds);
        foreach (var topicId in toAdd)
        {
            var mapping = GalleryPhotoTopicMapping.Create(command.PhotoId, topicId);
            await galleryRepository.AddTopicMappingAsync(mapping, cancellationToken);
        }

        return new AssignPhotoToTopicResult(photo.Id, command.PhotoTopicIds);
    }
}
