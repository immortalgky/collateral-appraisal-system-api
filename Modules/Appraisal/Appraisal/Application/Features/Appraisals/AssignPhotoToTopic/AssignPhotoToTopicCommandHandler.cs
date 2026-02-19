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

        photo.AssignToTopic(command.PhotoTopicId);

        await galleryRepository.UpdateAsync(photo, cancellationToken);

        return new AssignPhotoToTopicResult(photo.Id, photo.PhotoTopicId);
    }
}
