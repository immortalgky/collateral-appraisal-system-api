namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object representing the lifecycle status of an <see cref="ExternalEngagementCycle"/>.
/// Stored as a plain nvarchar column — same underlying data as the previous string property.
/// No schema migration required.
/// </summary>
public class CycleStatus : ValueObject
{
    public string Code { get; }

    private CycleStatus(string code) => Code = code;

    public static CycleStatus Open => new("Open");
    public static CycleStatus Closed => new("Closed");

    public static CycleStatus FromString(string code) => code switch
    {
        "Open" => Open,
        "Closed" => Closed,
        _ => throw new ArgumentException($"Invalid cycle status: {code}")
    };

    public static implicit operator string(CycleStatus status) => status.Code;

    public static bool operator ==(CycleStatus? left, CycleStatus? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(CycleStatus? left, CycleStatus? right) => !(left == right);

    public override bool Equals(object? obj) => obj is CycleStatus other && Code == other.Code;
    public override int GetHashCode() => Code.GetHashCode();
    public override string ToString() => Code;
}
