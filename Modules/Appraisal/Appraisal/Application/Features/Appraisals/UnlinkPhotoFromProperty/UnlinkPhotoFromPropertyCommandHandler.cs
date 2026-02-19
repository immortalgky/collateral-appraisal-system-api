using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnlinkPhotoFromProperty;

public class UnlinkPhotoFromPropertyCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<UnlinkPhotoFromPropertyCommand, UnlinkPhotoFromPropertyResult>
{
    public async Task<UnlinkPhotoFromPropertyResult> Handle(
        UnlinkPhotoFromPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var mapping = await galleryRepository.GetMappingByIdAsync(command.MappingId, cancellationToken);

        if (mapping is null)
            throw new InvalidOperationException($"Property photo mapping with ID {command.MappingId} not found");

        await galleryRepository.DeleteMappingAsync(mapping, cancellationToken);

        return new UnlinkPhotoFromPropertyResult(true);
    }
}
