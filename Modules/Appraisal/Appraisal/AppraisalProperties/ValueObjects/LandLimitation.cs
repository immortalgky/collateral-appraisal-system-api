namespace Appraisal.AppraisalProperties.ValueObjects;

public class LandLimitation : ValueObject
{
    public Expropriation Expropriation { get; } = default!;
    public Encroachment Encroachment { get; } = default!;
    public string? Electricity { get; }
    public decimal? ElectricityDistance { get; }
    public bool? IsLandlocked { get; }
    public string? IsLandlockedRemark { get; }
    public ForestBoundary ForestBoundary { get; } = default!;
    public string? LimitationOther { get; }

    private LandLimitation() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private LandLimitation(
        Expropriation expropriation,
        Encroachment encroachment,
        string? electricity,
        decimal? electricityDistance,
        bool? isLandlocked,
        string? isLandlockedRemark,
        ForestBoundary forestBoundary,
        string? limitationOther
    )
    {
        Expropriation = expropriation;
        Encroachment = encroachment;
        Electricity = electricity;
        ElectricityDistance = electricityDistance;
        IsLandlocked = isLandlocked;
        IsLandlockedRemark = isLandlockedRemark;
        ForestBoundary = forestBoundary;
        LimitationOther = limitationOther;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static LandLimitation Create(
        Expropriation expropriation,
        Encroachment encroachment,
        string? electricity,
        decimal? electricityDistance,
        bool? isLandlocked,
        string? isLandlockedRemark,
        ForestBoundary forestBoundary,
        string? limitationOther
    )
    {
        return new LandLimitation(
            expropriation,
            encroachment,
            electricity,
            electricityDistance,
            isLandlocked,
            isLandlockedRemark,
            forestBoundary,
            limitationOther
        );
    }
}
