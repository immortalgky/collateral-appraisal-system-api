namespace Appraisal.Domain.Appraisals;

public class AssignmentMethod : ValueObject
{
    public string Code { get; }

    private AssignmentMethod(string code)
    {
        Code = code;
    }

    public static AssignmentMethod Manual     => new("MANUAL");
    public static AssignmentMethod RoundRobin => new("ROUND_ROBIN");

    public static AssignmentMethod FromString(string code)
    {
        return code switch
        {
            "MANUAL"      => Manual,
            "ROUND_ROBIN" => RoundRobin,
            // Legacy values that may exist in older data
            "AUTO_RULE"   => AutoRule,
            "QUOTATION"   => Quotation,
            _ => throw new ArgumentException($"Invalid assignment method: {code}")
        };
    }

    // Legacy values — kept for backward-compatibility with existing data.
    // New assignments should use Manual or RoundRobin.
    public static AssignmentMethod AutoRule  => new("AUTO_RULE");
    public static AssignmentMethod Quotation => new("QUOTATION");

    public static implicit operator string(AssignmentMethod method) => method.Code;

    public override string ToString() => Code;
}
