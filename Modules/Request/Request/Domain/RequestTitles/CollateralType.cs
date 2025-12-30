using Request.Domain.RequestTitles.TitleTypes;

namespace Request.Domain.RequestTitles;

public sealed class CollateralType : ValueObject
{
    public string Code { get; }
    public string DisplayName { get; }
    private readonly Func<RequestTitleData, RequestTitle> _factory;

    private CollateralType(string code, string displayName, Func<RequestTitleData, RequestTitle> factory)
    {
        Code = code;
        DisplayName = displayName;
        _factory = factory;
    }

    public RequestTitle CreateTitle(RequestTitleData data)
    {
        return _factory(data);
    }

    public static readonly CollateralType Land = new("L", "Land", TitleLand.Create);
    public static readonly CollateralType Building = new("B", "Building", TitleBuilding.Create);
    public static readonly CollateralType LandAndBuilding = new("LB", "Land and Building", TitleLandBuilding.Create);
    public static readonly CollateralType Condo = new("U", "Condo", TitleCondo.Create);

    public static readonly CollateralType LeaseAgreementLand =
        new("LSL", "Lease Agreement Land", TitleLeaseAgreementLand.Create);

    public static readonly CollateralType LeaseAgreementBuilding =
        new("LSB", "Lease Agreement Building", TitleLeaseAgreementBuilding.Create);

    public static readonly CollateralType LeaseAgreementLandAndBuilding =
        new("LS", "Lease Agreement Land and Building", TitleLeaseAgreementLandBuilding.Create);

    public static readonly CollateralType LeaseAgreementCondo =
        new("LSU", "Lease Agreement Condo", TitleLeaseAgreementCondo.Create);

    public static readonly CollateralType Machine = new("MAC", "Machine", TitleMachine.Create);
    public static readonly CollateralType Vehicle = new("VEH", "Vehicle", TitleVehicle.Create);
    public static readonly CollateralType Vessel = new("VES", "Vessel", TitleVessel.Create);

    private static readonly CollateralType[] All =
    [
        Land,
        LeaseAgreementLand,
        Building,
        LeaseAgreementBuilding,
        LandAndBuilding,
        LeaseAgreementLandAndBuilding,
        Condo,
        LeaseAgreementCondo,
        Machine,
        Vehicle,
        Vessel
    ];

    public static CollateralType FromCode(string code)
    {
        return All.FirstOrDefault(t => t.Code == code)
               ?? throw new ArgumentException($"Invalid collateral type: {code}");
    }

    public static bool TryFromCode(string code, out CollateralType? type)
    {
        type = All.FirstOrDefault(t => t.Code == code);
        return type is not null;
    }

    public static IReadOnlyList<CollateralType> GetAll()
    {
        return All;
    }

    public static implicit operator string(CollateralType type)
    {
        return type.Code;
    }
}