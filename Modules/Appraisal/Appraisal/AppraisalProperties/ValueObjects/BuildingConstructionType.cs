namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingConstructionType : ValueObject
{
    public string? ConstType { get; }
    public string? ConstTypeOther { get; }

    private BuildingConstructionType(string? constType, string? constTypeOther)
    {
        ConstType = constType;
        ConstTypeOther = constTypeOther;
    }

    public static BuildingConstructionType Create(string? constType, string? constTypeOther)
    {
        return new BuildingConstructionType(constType, constTypeOther);
    }
}
