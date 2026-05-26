namespace Parameter.Parameters.Models;

public class Parameter : Aggregate<long>
{
    public string Group { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public string Language { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public int SeqNo { get; private set; }

    private Parameter()
    {
    }

    private Parameter(
        string group,
        string country,
        string language,
        string code,
        string description,
        bool isActive,
        int seqNo
    )
    {
        Group = group;
        Country = country;
        Language = language;
        Code = code;
        Description = description;
        IsActive = isActive;
        SeqNo = seqNo;
    }

    public static Parameter Create(
        string group,
        string country,
        string language,
        string code,
        string description,
        bool isActive,
        int seqNo
    )
    {
        return new Parameter(
            group,
            country,
            language,
            code,
            description,
            isActive,
            seqNo
        );
    }

    public void Update(
        string code,
        string description,
        string country,
        string language,
        bool isActive,
        int seqNo
    )
    {
        Code = code;
        Description = description;
        Country = country;
        Language = language;
        IsActive = isActive;
        SeqNo = seqNo;
    }
}