namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingGeneralStructure : ValueObject
{
    public string? GeneralStructure { get; }
    public string? GeneralStructureOther { get; }

    private BuildingGeneralStructure(string? generalStructure, string? generalStructureOther)
    {
        GeneralStructure = generalStructure;
        GeneralStructureOther = generalStructureOther;
    }

    public static BuildingGeneralStructure Create(
        string? generalStructure,
        string? generalStructureOther
    )
    {
        return new BuildingGeneralStructure(generalStructure, generalStructureOther);
    }
}
