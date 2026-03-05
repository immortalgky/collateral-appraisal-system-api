using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.SaveLawAndRegulations;

public class SaveLawAndRegulationsCommandHandler(
    ILawAndRegulationRepository repository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<SaveLawAndRegulationsCommand, SaveLawAndRegulationsResult>
{
    public async Task<SaveLawAndRegulationsResult> Handle(
        SaveLawAndRegulationsCommand command,
        CancellationToken cancellationToken)
    {
        var existing = (await repository.GetByAppraisalIdWithImagesAsync(
            command.AppraisalId, cancellationToken)).ToList();

        var inputIds = command.Items
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        // Delete regulations not in request
        var toDelete = existing.Where(e => !inputIds.Contains(e.Id)).ToList();
        var deletedPhotoIds = toDelete
            .SelectMany(r => r.Images)
            .Select(i => i.GalleryPhotoId)
            .Distinct()
            .ToList();

        if (toDelete.Count > 0)
            await repository.DeleteRangeAsync(toDelete, cancellationToken);

        // Mark orphaned photos as not in use
        foreach (var photoId in deletedPhotoIds)
        {
            var stillLinked = await galleryRepository.IsPhotoLinkedAnywhereAsync(photoId, cancellationToken);
            if (!stillLinked)
            {
                var photo = await galleryRepository.GetByIdAsync(photoId, cancellationToken);
                photo?.MarkAsNotInUse();
            }
        }

        // Update or create regulations
        foreach (var item in command.Items)
        {
            if (item.Id.HasValue)
            {
                var entity = existing.FirstOrDefault(e => e.Id == item.Id.Value);
                if (entity is null) continue;

                entity.Update(item.HeaderCode, item.Remark);
                await SyncImagesAsync(entity, item.Images, cancellationToken);
                await repository.UpdateAsync(entity, cancellationToken);
            }
            else
            {
                var entity = LawAndRegulation.Create(
                    command.AppraisalId, item.HeaderCode, item.Remark);

                foreach (var img in item.Images)
                {
                    entity.AddImage(
                        img.GalleryPhotoId, img.DisplaySequence,
                        img.Title, img.Description);

                    // Mark gallery photo as in use
                    var photo = await galleryRepository.GetByIdAsync(img.GalleryPhotoId, cancellationToken);
                    photo?.MarkAsInUse();
                }

                await repository.AddAsync(entity, cancellationToken);
            }
        }

        return new SaveLawAndRegulationsResult(
            command.AppraisalId,
            command.Items.Count,
            true);
    }

    private async Task SyncImagesAsync(
        LawAndRegulation entity,
        List<LawAndRegulationImageInput> imageInputs,
        CancellationToken cancellationToken)
    {
        var inputImageIds = imageInputs
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        // Remove images not in request
        var imagesToRemove = entity.Images
            .Where(i => !inputImageIds.Contains(i.Id))
            .ToList();

        var removedPhotoIds = imagesToRemove
            .Select(i => i.GalleryPhotoId)
            .Distinct()
            .ToList();

        foreach (var image in imagesToRemove)
            entity.RemoveImage(image.Id);

        // Mark orphaned photos as not in use
        foreach (var photoId in removedPhotoIds)
        {
            var stillLinked = await galleryRepository.IsPhotoLinkedAnywhereAsync(photoId, cancellationToken);
            if (!stillLinked)
            {
                var photo = await galleryRepository.GetByIdAsync(photoId, cancellationToken);
                photo?.MarkAsNotInUse();
            }
        }

        // Add new images (Id is null)
        foreach (var img in imageInputs.Where(i => !i.Id.HasValue))
        {
            entity.AddImage(
                img.GalleryPhotoId, img.DisplaySequence,
                img.Title, img.Description);

            // Mark gallery photo as in use
            var photo = await galleryRepository.GetByIdAsync(img.GalleryPhotoId, cancellationToken);
            photo?.MarkAsInUse();
        }

        // Update existing images
        foreach (var img in imageInputs.Where(i => i.Id.HasValue))
        {
            var existing = entity.Images.FirstOrDefault(e => e.Id == img.Id!.Value);
            if (existing is null) continue;

            existing.SetDisplaySequence(img.DisplaySequence);
            existing.SetMetadata(img.Title, img.Description);
        }
    }
}
