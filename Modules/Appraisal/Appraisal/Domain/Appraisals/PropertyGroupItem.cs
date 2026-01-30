namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Junction entity linking a property to a group.
/// Each property can only belong to one group (unique constraint).
/// </summary>
public class PropertyGroupItem : Entity<Guid>
{
    public Guid PropertyGroupId { get; private set; }
    public Guid AppraisalPropertyId { get; private set; }
    public int SequenceInGroup { get; private set; }

    // Private constructor for EF Core
    private PropertyGroupItem()
    {
    }

    // Private constructor for factory
    private PropertyGroupItem(
        Guid groupId,
        Guid propertyId,
        int sequenceInGroup)
    {
        // Id = Guid.NewGuid();
        PropertyGroupId = groupId;
        AppraisalPropertyId = propertyId;
        SequenceInGroup = sequenceInGroup;
    }

    /// <summary>
    /// Factory method to create a new group item
    /// </summary>
    public static PropertyGroupItem Create(
        Guid groupId,
        Guid propertyId,
        int sequenceInGroup)
    {
        return new PropertyGroupItem(groupId, propertyId, sequenceInGroup);
    }

    /// <summary>
    /// Update the sequence within the group (used when reordering)
    /// </summary>
    public void UpdateSequence(int newSequence)
    {
        SequenceInGroup = newSequence;
    }
}