namespace Collateral.CollateralProperties.Models;

public class LandTitle : Entity<long>
{
    public long CollatId { get; private set; }
    public int SeqNo { get; private set; }
    public LandTitleDocumentDetail LandTitleDocumentDetail { get; private set; } = default!;
    public LandTitleArea LandTitleArea { get; private set; } = default!;
    public string DocumentType { get; private set; } = default!;
    public string Rawang { get; private set; } = default!;
    public string? AerialPhotoNo { get; private set; }
    public string? BoundaryMarker { get; private set; }
    public string? BoundaryMarkerOther { get; private set; }
    public string DocValidate { get; private set; } = default!;
    public decimal? PricePerSquareWa { get; private set; }
    public decimal? GovernmentPrice { get; private set; }

    private LandTitle() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "SonarQube",
        "S107:Methods should not have too many parameters"
    )]
    private LandTitle(
        long collatId,
        int seqNo,
        LandTitleDocumentDetail landTitleDocumentDetail,
        LandTitleArea landTitleArea,
        string documentType,
        string rawang,
        string? aerialPhotoNo,
        string? boundaryMarker,
        string? boundaryMarkerOther,
        string docValidate,
        decimal? pricePerSquareWa,
        decimal? governmentPrice
    )
    {
        CollatId = collatId;
        SeqNo = seqNo;
        LandTitleDocumentDetail = landTitleDocumentDetail;
        LandTitleArea = landTitleArea;
        DocumentType = documentType;
        Rawang = rawang;
        AerialPhotoNo = aerialPhotoNo;
        BoundaryMarker = boundaryMarker;
        BoundaryMarkerOther = boundaryMarkerOther;
        DocValidate = docValidate;
        PricePerSquareWa = pricePerSquareWa;
        GovernmentPrice = governmentPrice;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "SonarQube",
        "S107:Methods should not have too many parameters"
    )]
    public static LandTitle Create(
        long collatId,
        int seqNo,
        LandTitleDocumentDetail landTitleDocumentDetail,
        LandTitleArea landTitleArea,
        string documentType,
        string rawang,
        string? aerialPhotoNo,
        string? boundaryMarker,
        string? boundaryMarkerOther,
        string docValidate,
        decimal? pricePerSquareWa,
        decimal? governmentPrice
    )
    {
        return new LandTitle(
            collatId,
            seqNo,
            landTitleDocumentDetail,
            landTitleArea,
            documentType,
            rawang,
            aerialPhotoNo,
            boundaryMarker,
            boundaryMarkerOther,
            docValidate,
            pricePerSquareWa,
            governmentPrice
        );
    }

    public void Update(LandTitle landTitle)
    {
        if (!SeqNo.Equals(landTitle.SeqNo))
        {
            SeqNo = landTitle.SeqNo;
        }

        if (!LandTitleDocumentDetail.Equals(landTitle.LandTitleDocumentDetail))
        {
            LandTitleDocumentDetail = landTitle.LandTitleDocumentDetail;
        }

        if (!LandTitleArea.Equals(landTitle.LandTitleArea))
        {
            LandTitleArea = landTitle.LandTitleArea;
        }

        if (!DocumentType.Equals(landTitle.DocumentType))
        {
            DocumentType = landTitle.DocumentType;
        }

        if (!Rawang.Equals(landTitle.Rawang))
        {
            Rawang = landTitle.Rawang;
        }

        if (AerialPhotoNo is null || !AerialPhotoNo.Equals(landTitle.AerialPhotoNo))
        {
            AerialPhotoNo = landTitle.AerialPhotoNo;
        }

        if (BoundaryMarker is null || !BoundaryMarker.Equals(landTitle.BoundaryMarker))
        {
            BoundaryMarker = landTitle.BoundaryMarker;
        }

        if (
            BoundaryMarkerOther is null
            || !BoundaryMarkerOther.Equals(landTitle.BoundaryMarkerOther)
        )
        {
            BoundaryMarkerOther = landTitle.BoundaryMarkerOther;
        }

        if (!DocValidate.Equals(landTitle.DocValidate))
        {
            DocValidate = landTitle.DocValidate;
        }

        if (PricePerSquareWa is null || !PricePerSquareWa.Equals(landTitle.PricePerSquareWa))
        {
            PricePerSquareWa = landTitle.PricePerSquareWa;
        }

        if (GovernmentPrice is null || !GovernmentPrice.Equals(landTitle.GovernmentPrice))
        {
            GovernmentPrice = landTitle.GovernmentPrice;
        }
    }
}
