namespace Request.Requests.ValueObjects;

public class RequestTitle : ValueObject
{

    private readonly List<TitleDocument> _titleDocuments = [];
    public RequestTitle()
    {
    }

    private RequestTitle(
        Collateral collateral,
        Area area,
        Condo condo,
        TitleAddress titleAddress,
        DopaAddress dopaAddress,
        Building building,
        Vehicle vehicle,
        Machine machine
    )
    {
        Collateral = collateral;
        Area = area;
        Condo = condo;
        TitleAddress = titleAddress;
        DopaAddress = dopaAddress;
        Building = building;
        Vehicle = vehicle;
        Machine = machine;
    }

    public IReadOnlyList<TitleDocument> TitleDocuments => _titleDocuments.AsReadOnly();

    public Collateral Collateral { get; } = default!;
    public Area Area { get; } = default!;
    public Condo Condo { get; } = default!;
    public TitleAddress TitleAddress { get; } = default!;
    public DopaAddress DopaAddress { get; } = default!;
    public Building Building { get; } = default!;
    public Vehicle Vehicle { get; } = default!;
    public Machine Machine { get; } = default!;

    public static RequestTitle Of(Collateral collateral, Area area, Condo condo, TitleAddress titleAddress,
        DopaAddress dopaAddress, Building building, Vehicle vehicle, Machine machine)
    {
        ArgumentNullException.ThrowIfNull(collateral);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(condo);
        ArgumentNullException.ThrowIfNull(titleAddress);
        ArgumentNullException.ThrowIfNull(dopaAddress);
        ArgumentNullException.ThrowIfNull(building);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(machine);

        return new RequestTitle(collateral, area, condo, titleAddress, dopaAddress, building, vehicle, machine);
    }
}