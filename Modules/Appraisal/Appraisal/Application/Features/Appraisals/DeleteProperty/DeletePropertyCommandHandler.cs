using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.DeleteProperty;

public class DeletePropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalGalleryRepository galleryRepository,
    PricingReferenceCleanupService cleanupService,
    AppraisalValuationSummaryService valuationSummaryService
) : ICommandHandler<DeletePropertyCommand, DeletePropertyResult>
{
    public async Task<DeletePropertyResult> Handle(DeletePropertyCommand command, CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.appraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.appraisalId);

        // Existence check only, so a missing property surfaces as PropertyNotFoundException
        // instead of the InvalidOperationException RemoveProperty would throw further down.
        _ = appraisal.GetProperty(command.propertyId)
            ?? throw new PropertyNotFoundException(command.propertyId);

        // Active cleanup: delete MachineryCostRef PricingAnalyses anchored to this property (DL10)
        // plus any MachineCostItems that reference it.
        await cleanupService.CleanupForPropertyAsync(command.propertyId, cancellationToken);

        // PropertyPhotoMappings has a Restrict FK to AppraisalProperties — it must be cleared
        // explicitly or the delete fails with SQL 547.
        await RemovePhotoMappingsAsync(command.propertyId, cancellationToken);

        // Removing the property from the aggregate is what deletes the row: AppraisalProperty.AppraisalId
        // is non-nullable, so EF treats the relationship as required and cascade-deletes the orphan.
        appraisal.RemoveProperty(command.propertyId);

        // Flush the property removal BEFORE recomputing so RecomputeAsync's insurance sum (over
        // AppraisalProperties.BuildingDetail/CondoDetail) no longer counts the deleted structure.
        // RemoveProperty raises no domain event, so without this explicit recompute the appraisal
        // ValuationAnalyses insurance total stays stale until an unrelated pricing edit — mirroring
        // DeletePropertyGroupCommandHandler. The recompute's upsert/outbox commit inside
        // TransactionalBehavior's transaction (DeletePropertyCommand is ITransactionalCommand).
        await appraisalRepository.SaveChangesAsync(cancellationToken);
        await valuationSummaryService.RecomputeAsync(command.appraisalId, cancellationToken);

        return new DeletePropertyResult(IsSuccess: true);
    }

    /// <summary>
    /// Unlinks every gallery photo from the property being deleted, then marks any photo that
    /// lost its last link as no longer in use — mirroring UnlinkPhotoFromPropertyCommandHandler.
    /// Without the IsInUse bookkeeping, such photos stay flagged in-use and cannot be removed
    /// from the gallery. Thumbnail promotion is not needed here: the owning property is going away.
    /// </summary>
    private async Task RemovePhotoMappingsAsync(Guid propertyId, CancellationToken cancellationToken)
    {
        var mappings = (await galleryRepository.GetMappingsByPropertyIdAsync(propertyId, cancellationToken))
            .ToList();

        if (mappings.Count == 0) return;

        var photoIds = mappings.Select(m => m.GalleryPhotoId).Distinct().ToList();

        foreach (var mapping in mappings)
            await galleryRepository.DeleteMappingAsync(mapping, cancellationToken);

        // Batched: one linked-anywhere check (which flushes the deletes) plus one load of the
        // orphaned photos, instead of two round-trips + a hidden flush per photo.
        var stillLinked = await galleryRepository.GetPhotosLinkedElsewhereAsync(photoIds, cancellationToken);
        var orphanedIds = photoIds.Where(id => !stillLinked.Contains(id)).ToList();

        if (orphanedIds.Count == 0) return;

        foreach (var photo in await galleryRepository.GetByIdsAsync(orphanedIds, cancellationToken))
            photo.MarkAsNotInUse();
    }
}
