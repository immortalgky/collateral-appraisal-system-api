namespace Parameter.Parameters.Models;

public class Parameter : Aggregate<long>
{
    public string Group { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public string Language { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Active { get; private set; } = default!;
    public string SeqNo { get; private set; } = default!;

    private Parameter()
    {
    }

    private Parameter(
        string group,
        string country,
        string language,
        string code,
        string description,
        string active,
        string seqNo
    )
    {
        Group = group;
        Country = country;
        Language = language;
        Code = code;
        Description = description;
        Active = active;
        SeqNo = seqNo;
    }

    public static Parameter Create(
        string group,
        string country,
        string language,
        string code,
        string description,
        string active,
        string seqNo
    )
    {
        return new Parameter(
            group,
            country,
            language,
            code,
            description,
            active,
            seqNo
        );
    }

}