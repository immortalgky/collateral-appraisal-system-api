namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Canonical document type constants for CollateralDocument.
/// </summary>
public static class DocumentTypes
{
    public const string TitleDeed = "TitleDeed";
    public const string LeaseContract = "LeaseContract";
    public const string OwnershipCertificate = "OwnershipCertificate";
    public const string EncumbranceLetter = "EncumbranceLetter";
    public const string Other = "Other";

    private static readonly HashSet<string> _all =
    [
        TitleDeed,
        LeaseContract,
        OwnershipCertificate,
        EncumbranceLetter,
        Other
    ];

    public static bool IsValid(string documentType) => _all.Contains(documentType);
}
