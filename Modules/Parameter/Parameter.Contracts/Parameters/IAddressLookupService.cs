using Parameter.Contracts.Parameters.Dtos;

namespace Parameter.Contracts.Parameters;

/// <summary>
/// Outcome of resolving free-text province/district/sub-district names into a geocode.
/// </summary>
public enum AddressResolutionStatus
{
    /// <summary>Nothing was supplied — caller decides whether that is an error.</summary>
    Empty,

    /// <summary>Exactly one address matched.</summary>
    Ok,

    /// <summary>No address matched the supplied names/codes.</summary>
    NotFound,

    /// <summary>More than one address matched — caller must ask the user to narrow it down.</summary>
    Ambiguous
}

/// <param name="Status">See <see cref="AddressResolutionStatus"/>.</param>
/// <param name="Matched">The single match when <paramref name="Status"/> is Ok, otherwise null.</param>
/// <param name="Candidates">
/// Up to a handful of near-misses, so the caller can render "did you mean …" in an error message.
/// Populated for Ambiguous; may also be populated for NotFound when the province/district resolved
/// but the sub-district did not.
/// </param>
public record AddressResolution(
    AddressResolutionStatus Status,
    AddressDto? Matched,
    IReadOnlyList<AddressDto> Candidates
)
{
    public static readonly AddressResolution Empty =
        new(AddressResolutionStatus.Empty, null, []);

    public static AddressResolution Found(AddressDto match) =>
        new(AddressResolutionStatus.Ok, match, []);

    public static AddressResolution NotFound(IReadOnlyList<AddressDto>? candidates = null) =>
        new(AddressResolutionStatus.NotFound, null, candidates ?? []);

    public static AddressResolution Ambiguous(IReadOnlyList<AddressDto> candidates) =>
        new(AddressResolutionStatus.Ambiguous, null, candidates);
}

/// <summary>
/// Resolves human-typed Thai address names (or a raw geocode) into the canonical
/// <see cref="AddressDto"/> — province/district/sub-district codes plus postcode.
///
/// Needed because the request/collateral tables store the 6-digit sub-district GEOCODE in the
/// SubDistrict column, never the Thai name, while spreadsheets and integration payloads carry names.
///
/// Both masters are supported and are NOT interchangeable: the Title dataset (Land Department) is
/// larger and messier than the DOPA dataset. Pick the one matching the field you are filling.
/// </summary>
public interface IAddressLookupService
{
    /// <summary>Resolve against the Title (Land Department) address master.</summary>
    Task<AddressResolution> ResolveTitleAsync(
        string? province,
        string? district,
        string? subDistrict,
        CancellationToken cancellationToken);

    /// <summary>Resolve against the DOPA address master.</summary>
    Task<AddressResolution> ResolveDopaAsync(
        string? province,
        string? district,
        string? subDistrict,
        CancellationToken cancellationToken);
}
