using Shared.DDD;

namespace Appraisal.Domain.Projects;

/// <summary>
/// Value object representing the type of a Block/Project aggregate.
/// The <see cref="Code"/> ("U"/"LB"/"L") IS the wire format and the DB representation,
/// so DTOs carry it as a plain <c>string</c> — there is no JSON converter and no enum.
/// Mirrors the <c>PropertyType</c> value-object pattern.
/// </summary>
public class ProjectType : ValueObject
{
    public string Code { get; }

    private ProjectType(string code)
    {
        Code = code;
    }

    // Predefined types
    public static ProjectType Condo => new("U");
    public static ProjectType LandAndBuilding => new("LB");
    public static ProjectType Land => new("L");

    /// <summary>
    /// Returns true for both LandAndBuilding and Land. Both share the same domain logic
    /// (models, unit structure, pricing) in v1.
    /// // TODO(Land): When Land-specific business rules are needed, split this check.
    /// </summary>
    public bool IsLandAndBuildingLike() => Code is "LB" or "L";

    /// <summary>The short text code — kept as a method for EF config / call-site stability.</summary>
    public string ToCode() => Code;

    public static ProjectType FromCode(string code) => FromString(code);

    public static ProjectType FromString(string code) => code switch
    {
        "U" => Condo,
        "LB" => LandAndBuilding,
        "L" => Land,
        _ => throw new ArgumentException($"Invalid project type: {code}", nameof(code))
    };

    // Boundary-safe predicates for validators — tolerate null/invalid input, never throw.
    public static bool IsValidCode(string? code) => code is "U" or "LB" or "L";
    public static bool IsLandAndBuildingLikeCode(string? code) => code is "LB" or "L";
    public static bool IsCondoCode(string? code) => code == "U";

    public static IReadOnlyList<ProjectType> All => [Condo, LandAndBuilding, Land];

    // Implicit conversion to string (Code is the wire format)
    public static implicit operator string(ProjectType type) => type.Code;

    // Equality by Code. Intentionally shadows the base ValueObject == (which has empty-aware
    // null semantics): callers hold this value typed as ProjectType, so overload resolution
    // picks these. Mirrors PropertyType. Don't compare instances typed as ValueObject.
    public static bool operator ==(ProjectType? left, ProjectType? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(ProjectType? left, ProjectType? right) => !(left == right);

    public override bool Equals(object? obj) => obj is ProjectType other && Code == other.Code;

    public override int GetHashCode() => Code.GetHashCode();

    public override string ToString() => Code;
}
