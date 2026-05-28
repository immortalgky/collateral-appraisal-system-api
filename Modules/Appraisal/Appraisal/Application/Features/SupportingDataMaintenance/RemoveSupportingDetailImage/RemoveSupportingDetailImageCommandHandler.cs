using Appraisal.Infrastructure.Repositories;
using Shared.CQRS;

namespace Appraisal.Application.Features.SupportingDataMaintenance.RemoveSupportingDetailImage;

public class RemoveSupportingDetailImageCommandHandler(ISupportingDataRepository repo)
    : ICommandHandler<RemoveSupportingDetailImageCommand, RemoveSupportingDetailImageResult>
{
    public async Task<RemoveSupportingDetailImageResult> Handle(
        RemoveSupportingDetailImageCommand command,
        CancellationToken cancellationToken)
    {
        var detail = await repo.GetDetailByIdWithImagesAsync(command.DetailId, cancellationToken);

        if (detail is null || detail.SupportingDataId != command.SupportingId)
            throw new InvalidOperationException(
                $"Supporting detail {command.DetailId} not found under supporting data {command.SupportingId}.");

        detail.RemoveImage(command.ImageId);

        // No SaveChangesAsync — TransactionalBehavior handles the commit.
        return new RemoveSupportingDetailImageResult(true);
    }
}
