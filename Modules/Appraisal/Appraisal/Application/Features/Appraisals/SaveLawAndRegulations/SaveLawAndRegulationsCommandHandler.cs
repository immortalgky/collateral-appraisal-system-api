using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.SaveLawAndRegulations;

public class SaveLawAndRegulationsCommandHandler(
    ILawAndRegulationRepository repository
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
        if (toDelete.Count > 0)
            await repository.DeleteRangeAsync(toDelete, cancellationToken);

        // Update or create regulations
        foreach (var item in command.Items)
        {
            if (item.Id.HasValue)
            {
                var entity = existing.FirstOrDefault(e => e.Id == item.Id.Value);
                if (entity is null) continue;

                entity.Update(item.HeaderCode, item.Remark);
                SyncImages(entity, item.Images);
                await repository.UpdateAsync(entity, cancellationToken);
            }
            else
            {
                var entity = LawAndRegulation.Create(
                    command.AppraisalId, item.HeaderCode, item.Remark);

                foreach (var img in item.Images)
                {
                    entity.AddImage(
                        img.DocumentId, img.DisplaySequence,
                        img.FileName, img.FilePath, img.Title, img.Description);
                }

                await repository.AddAsync(entity, cancellationToken);
            }
        }

        return new SaveLawAndRegulationsResult(
            command.AppraisalId,
            command.Items.Count,
            true);
    }

    private static void SyncImages(LawAndRegulation entity, List<LawAndRegulationImageInput> imageInputs)
    {
        var inputImageIds = imageInputs
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        // Remove images not in request
        var imagesToRemove = entity.Images
            .Where(i => !inputImageIds.Contains(i.Id))
            .Select(i => i.Id)
            .ToList();

        foreach (var imageId in imagesToRemove)
            entity.RemoveImage(imageId);

        // Add new images (Id is null)
        foreach (var img in imageInputs.Where(i => !i.Id.HasValue))
        {
            entity.AddImage(
                img.DocumentId, img.DisplaySequence,
                img.FileName, img.FilePath, img.Title, img.Description);
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
