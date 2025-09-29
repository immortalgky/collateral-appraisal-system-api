namespace Shared.Dtos;

public record LandTitleDto(
    long Id,
    int SeqNo,
    LandTitleDocumentDetailDto LandTitleDocumentDetail,
    LandTitleAreaDto LandTitleArea,
    string DocumentType,
    string Rawang,
    string? AerialPhotoNo,
    string? BoundaryMarker,
    string? BoundaryMarkerOther,
    string DocValidate,
    decimal? PricePerSquareWa,
    decimal? GovernmentPrice
);
