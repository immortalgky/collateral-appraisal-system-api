namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingRoofFrame : ValueObject
{
    public string? RoofFrame { get; }
    public string? RoofFrameOther { get; }

    private BuildingRoofFrame(string? roofFrame, string? roofFrameOther)
    {
        RoofFrame = roofFrame;
        RoofFrameOther = roofFrameOther;
    }

    public static BuildingRoofFrame Create(string? roofFrame, string? roofFrameOther)
    {
        return new BuildingRoofFrame(roofFrame, roofFrameOther);
    }
}
