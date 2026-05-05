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

    public string FamilyCode => Code switch
    {
        "01" or "13" or "14" or "17" or "19" or "21" or "26" or "27" => "L",
        "02" or "03" or "04" or "23" or "24" or "32" => "LB",
        "05" or "06" or "07" or "15" or "16" or "18" or "20" or "22" => "B",
        "08" or "33" => "U",
        "09" or "25" or "30" or "31" => "LS",
        "29" => "LSL",
        "28" => "LSU",
        "10" => "VEH",
        "11" => "MAC",
        "12" => "VES",
        _ => Code
    };

    // Land family (01, 13, 14, 17, 19, 21, 26, 27)
    public static readonly CollateralType Code01 = new("01", "Land", TitleLand.Create);
    public static readonly CollateralType Code13 = new("13", "Land (Part 1)", TitleLand.Create);
    public static readonly CollateralType Code14 = new("14", "Land (Part 2)", TitleLand.Create);
    public static readonly CollateralType Code17 = new("17", "Land (Part 2)", TitleLand.Create);
    public static readonly CollateralType Code19 = new("19", "Land (Group 1)", TitleLand.Create);
    public static readonly CollateralType Code21 = new("21", "Land (Group 2)", TitleLand.Create);
    public static readonly CollateralType Code26 = new("26", "Land (Group 3)", TitleLand.Create);
    public static readonly CollateralType Code27 = new("27", "Land (Group 4)", TitleLand.Create);

    // Land with buildings family (02, 03, 04, 23, 24, 32)
    public static readonly CollateralType Code02 = new("02", "Land with buildings", TitleLandBuilding.Create);
    public static readonly CollateralType Code03 = new("03", "Land with buildings (blueprint)", TitleLandBuilding.Create);
    public static readonly CollateralType Code04 = new("04", "Land allocation (whole project)", TitleLandBuilding.Create);
    public static readonly CollateralType Code23 = new("23", "Land with buildings (Group 1)", TitleLandBuilding.Create);
    public static readonly CollateralType Code24 = new("24", "Land with buildings (Group 2)", TitleLandBuilding.Create);
    public static readonly CollateralType Code32 = new("32", "Land with buildings (BlockLand)", TitleLandBuilding.Create);

    // Building family (05, 06, 07, 15, 16, 18, 20, 22)
    public static readonly CollateralType Code05 = new("05", "Buildings", TitleBuilding.Create);
    public static readonly CollateralType Code06 = new("06", "Building (blueprint)", TitleBuilding.Create);
    public static readonly CollateralType Code07 = new("07", "Building (whole project)", TitleBuilding.Create);
    public static readonly CollateralType Code15 = new("15", "Building (Part 1)", TitleBuilding.Create);
    public static readonly CollateralType Code16 = new("16", "Building (Part 2)", TitleBuilding.Create);
    public static readonly CollateralType Code18 = new("18", "Building (Part 2)", TitleBuilding.Create);
    public static readonly CollateralType Code20 = new("20", "Building (Group 1)", TitleBuilding.Create);
    public static readonly CollateralType Code22 = new("22", "Building (Group 2)", TitleBuilding.Create);

    // Condo family (08, 33)
    public static readonly CollateralType Code08 = new("08", "Apartment", TitleCondo.Create);
    public static readonly CollateralType Code33 = new("33", "Condominium (BlockCondo)", TitleCondo.Create);

    // Lease agreement land+building family (09, 25, 30, 31)
    public static readonly CollateralType Code09 = new("09", "Leasehold rights, real estate", TitleLeaseAgreementLandBuilding.Create);
    public static readonly CollateralType Code25 = new("25", "Leasehold rights (land with buildings)", TitleLeaseAgreementLandBuilding.Create);
    public static readonly CollateralType Code30 = new("30", "Leasehold rights", TitleLeaseAgreementLandBuilding.Create);
    public static readonly CollateralType Code31 = new("31", "Lease rights for space within shopping center", TitleLeaseAgreementLandBuilding.Create);

    // Lease agreement land (29)
    public static readonly CollateralType Code29 = new("29", "Land lease rights", TitleLeaseAgreementLand.Create);

    // Lease agreement condo (28)
    public static readonly CollateralType Code28 = new("28", "Leasehold rights (condominium)", TitleLeaseAgreementCondo.Create);

    // Movables (10, 11, 12)
    public static readonly CollateralType Code10 = new("10", "Car", TitleVehicle.Create);
    public static readonly CollateralType Code11 = new("11", "Machinery", TitleMachine.Create);
    public static readonly CollateralType Code12 = new("12", "Ship", TitleVessel.Create);

    private static readonly CollateralType[] All =
    [
        Code01, Code02, Code03, Code04, Code05, Code06, Code07, Code08, Code09,
        Code10, Code11, Code12, Code13, Code14, Code15, Code16, Code17, Code18,
        Code19, Code20, Code21, Code22, Code23, Code24, Code25, Code26, Code27,
        Code28, Code29, Code30, Code31, Code32, Code33
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