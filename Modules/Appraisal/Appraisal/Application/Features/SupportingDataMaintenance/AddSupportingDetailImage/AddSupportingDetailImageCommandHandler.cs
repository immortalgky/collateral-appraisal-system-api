using Appraisal.Infrastructure.Repositories;
using Shared.CQRS;

namespace Appraisal.Application.Features.SupportingDataMaintenance.AddSupportingDetailImage;

public class AddSupportingDetailImageCommandHandler(ISupportingDataRepository repo)
    : ICommandHandler<AddSupportingDetailImageCommand, AddSupportingDetailImageResult>
{
    public async Task<AddSupportingDetailImageResult> Handle(
        AddSupportingDetailImageCommand command,
        CancellationToken cancellationToken)
    {
        var (detail, status) = await repo.GetDetailByIdWithImagesAsync(command.DetailId, cancellationToken);

        if (detail is null || detail.SupportingDataId != command.SupportingId)
            throw new NotFoundException("SupportingDataDetail", command.DetailId);

        var image = detail.AddImage(
            command.DocumentId,
            command.StorageUrl,
            command.FileName,
            command.Title,
            command.Description);

        // No SaveChangesAsync — TransactionalBehavior handles the commit.
        return new AddSupportingDetailImageResult(image.Id);
    }
}
