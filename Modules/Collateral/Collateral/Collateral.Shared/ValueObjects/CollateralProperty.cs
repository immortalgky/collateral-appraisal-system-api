namespace Collateral.Collateral.Shared.ValueObjects;

public class CollateralProperty : ValueObject
{
    public string? Name { get; }
    public string? Brand { get; }
    public string? Model { get; }
    public string? EnergyUse { get; }

    private CollateralProperty() { }
    private CollateralProperty(
        string? name,
        string? brand,
        string? model,
        string? energyUse
    )
    {
        Name = name;
        Brand = brand;
        Model = model;
        EnergyUse = energyUse;
    }

    public static CollateralProperty Create(
        string? name,
        string? brand,
        string? model,
        string? energyUse
    )
    {
        return new CollateralProperty(name, brand, model, energyUse);
    }
}