namespace Appraisal.AppraisalProperties.ValueObjects;

public class CondoFloor : ValueObject
{
    public string? GroundFloorMaterial { get; }
    public string? GroundFloorMaterialOther { get; }
    public string? UpperFloorMaterial { get; }
    public string? UpperFloorMaterialOther { get; }
    public string? BathroomFloorMaterial { get; }
    public string? BathroomFloorMaterialOther { get; }

    private CondoFloor(
        string? groundFloorMaterial,
        string? groundFloorMaterialOther,
        string? upperFloorMaterial,
        string? upperFloorMaterialOther,
        string? bathroomFloorMaterial,
        string? bathroomFloorMaterialOther
    )
    {
        GroundFloorMaterial = groundFloorMaterial;
        GroundFloorMaterialOther = groundFloorMaterialOther;
        UpperFloorMaterial = upperFloorMaterial;
        UpperFloorMaterialOther = upperFloorMaterialOther;
        BathroomFloorMaterial = bathroomFloorMaterial;
        BathroomFloorMaterialOther = bathroomFloorMaterialOther;
    }

    public static CondoFloor Create(
        string? groundFloorMaterial,
        string? groundFloorMaterialOther,
        string? upperFloorMaterial,
        string? upperFloorMaterialOther,
        string? bathroomFloorMaterial,
        string? bathroomFloorMaterialOther
    )
    {
        return new CondoFloor(
            groundFloorMaterial,
            groundFloorMaterialOther,
            upperFloorMaterial,
            upperFloorMaterialOther,
            bathroomFloorMaterial,
            bathroomFloorMaterialOther
        );
    }
}
