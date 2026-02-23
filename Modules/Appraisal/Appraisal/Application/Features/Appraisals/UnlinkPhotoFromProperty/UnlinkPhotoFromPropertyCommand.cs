using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnlinkPhotoFromProperty;

public record UnlinkPhotoFromPropertyCommand(
    Guid MappingId
) : ICommand<UnlinkPhotoFromPropertyResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
