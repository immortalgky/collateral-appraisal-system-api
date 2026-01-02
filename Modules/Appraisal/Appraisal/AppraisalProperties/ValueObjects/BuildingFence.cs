namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingFence : ValueObject
{
    public string? Fence { get; }
    public string? FenceOther { get; }

    private BuildingFence(string? fence, string? fenceOther)
    {
        Fence = fence;
        FenceOther = fenceOther;
    }

    public static BuildingFence Create(string? fence, string? fenceOther)
    {
        return new BuildingFence(fence, fenceOther);
    }
}
