using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetGalleryPhotos;

public record GetGalleryPhotosQuery(Guid AppraisalId) : IQuery<GetGalleryPhotosResult>;
