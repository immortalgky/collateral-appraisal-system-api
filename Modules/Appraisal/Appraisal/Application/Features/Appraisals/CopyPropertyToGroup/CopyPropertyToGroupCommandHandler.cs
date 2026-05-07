using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.CopyPropertyToGroup;

public class CopyPropertyToGroupCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalDbContext dbContext,
    ICurrentUserService currentUser
) : ICommandHandler<CopyPropertyToGroupCommand, CopyPropertyToGroupResult>
{
    public async Task<CopyPropertyToGroupResult> Handle(
        CopyPropertyToGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // Step 1: Copy property and save so EF Core generates the ID
        var newProperty = appraisal.CopyProperty(command.SourcePropertyId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Step 2: Duplicate PropertyPhotoMapping rows so the copied property carries forward
        // the photos linked to the source. We reuse the same GalleryPhotoId (no blob copy);
        // each mapping is a join row pointing at the same gallery photo.
        await CopyPhotoMappingsAsync(
            command.SourcePropertyId, newProperty.Id, cancellationToken);

        // Step 3: Now newProperty.Id is valid — assign to group and save
        appraisal.AddPropertyToGroup(command.TargetGroupId, newProperty.Id);

        return new CopyPropertyToGroupResult(newProperty.Id);
    }

    private async Task CopyPhotoMappingsAsync(
        Guid sourcePropertyId,
        Guid newPropertyId,
        CancellationToken ct)
    {
        var sourceMappings = await dbContext.PropertyPhotoMappings
            .AsNoTracking()
            .Where(m => m.AppraisalPropertyId == sourcePropertyId)
            .ToListAsync(ct);

        if (sourceMappings.Count == 0)
            return;

        var linkedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? "System";
        foreach (var src in sourceMappings)
        {
            var copy = PropertyPhotoMapping.Create(
                galleryPhotoId: src.GalleryPhotoId,
                appraisalPropertyId: newPropertyId,
                photoPurpose: src.PhotoPurpose,
                linkedBy: linkedBy);
            copy.SetSection(src.SectionReference);
            copy.SetSequence(src.SequenceNumber);
            if (src.IsThumbnail)
                copy.SetAsThumbnail();
            dbContext.PropertyPhotoMappings.Add(copy);
        }
    }
}