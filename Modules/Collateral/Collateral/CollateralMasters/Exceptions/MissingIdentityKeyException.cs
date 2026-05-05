namespace Collateral.CollateralMasters.Exceptions;

/// <summary>
/// Thrown by the upsert service when a property is missing required identity/dedup fields.
/// Causes MassTransit to dead-letter the message.
/// </summary>
public class MissingIdentityKeyException(Guid propertyId, string propertyType, IReadOnlyList<string> missingFields)
    : Exception($"Property {propertyId} (type={propertyType}) is missing required identity fields: {string.Join(", ", missingFields)}")
{
    public Guid PropertyId { get; } = propertyId;
    public string PropertyType { get; } = propertyType;
    public IReadOnlyList<string> MissingFields { get; } = missingFields;
}
