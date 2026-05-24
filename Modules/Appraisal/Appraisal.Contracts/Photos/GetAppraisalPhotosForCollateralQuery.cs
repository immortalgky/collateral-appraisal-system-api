using MediatR;

namespace Appraisal.Contracts.Photos;

/// <summary>
/// Cross-module query: returns the gallery photos for a specific appraisal,
/// optionally filtered to a set of AppraisalProperty IDs.
///
/// Used by the Collateral module to resolve photos via the latest CollateralEngagement →
/// AppraisalId → AppraisalProperty IDs stored in the engagement snapshot.
///
/// PropertyIds: when non-empty, only photos mapped to those properties are returned
/// (via appraisal.PropertyPhotoMappings). When empty, ALL photos for the appraisal
/// that are marked IsInUse = 1 are returned.
/// </summary>
public record GetAppraisalPhotosForCollateralQuery(
    Guid AppraisalId,
    IReadOnlyList<Guid> PropertyIds
) : IRequest<IReadOnlyList<CollateralPhotoDto>>;

/// <summary>
/// A single photo entry returned for a CollateralMaster's gallery.
///
/// Url: derived from the FilePath column stored on AppraisalGallery (denormalized from the
/// Document module at upload time). No live Document module call is made — the FilePath is
/// the authoritative access path. Returns null when FilePath was not captured at upload time.
/// </summary>
public record CollateralPhotoDto(
    Guid DocumentId,
    string PhotoType,
    string? PhotoCategory,
    string? Caption,
    string? Url,
    int Sequence,
    Guid PropertyId
);
