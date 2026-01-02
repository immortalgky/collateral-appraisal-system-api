namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingConstructionStyle : ValueObject
{
    public string? ConstStyle { get; }
    public string? ConstStyleRemark { get; }

    private BuildingConstructionStyle(string? constStyle, string? constStyleRemark)
    {
        ConstStyle = constStyle;
        ConstStyleRemark = constStyleRemark;
    }

    public static BuildingConstructionStyle Create(string? constStyle, string? constStyleRemark)
    {
        return new BuildingConstructionStyle(constStyle, constStyleRemark);
    }
}
