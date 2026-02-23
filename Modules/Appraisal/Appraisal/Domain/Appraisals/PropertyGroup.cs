namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Entity representing a group of properties for combined valuation.
/// Multiple properties can be grouped together (e.g., Land + Building on same plot).
/// </summary>
public class PropertyGroup : Entity<Guid>
{
    private readonly List<PropertyGroupItem> _items = [];

    // Core Properties
    public Guid AppraisalId { get; private set; }
    public int GroupNumber { get; private set; }
    public string GroupName { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool UseSystemCalc { get; private set; } = true; // Use system pricing analysis

    // Read-only accessor for items
    public IReadOnlyList<PropertyGroupItem> Items => _items.AsReadOnly();

    // Private constructor for EF Core
    private PropertyGroup()
    {
    }

    // Private constructor for factory
    private PropertyGroup(
        Guid appraisalId,
        int groupNumber,
        string groupName,
        string? description)
    {
        //Id = Guid.NewGuid();
        AppraisalId = appraisalId;
        GroupNumber = groupNumber;
        GroupName = groupName;
        Description = description;
    }

    /// <summary>
    /// Factory method to create a new group
    /// </summary>
    public static PropertyGroup Create(
        Guid appraisalId,
        int groupNumber,
        string groupName,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        return new PropertyGroup(appraisalId, groupNumber, groupName, description);
    }

    /// <summary>
    /// Add a property to this group
    /// </summary>
    public PropertyGroupItem AddProperty(Guid propertyId)
    {
        // Check if already in this group
        if (_items.Any(i => i.AppraisalPropertyId == propertyId))
            throw new InvalidOperationException($"Property {propertyId} is already in this group");

        var sequenceInGroup = _items.Count + 1;
        var item = PropertyGroupItem.Create(Id, propertyId, sequenceInGroup);
        _items.Add(item);

        return item;
    }

    /// <summary>
    /// Remove a property from this group
    /// </summary>
    public void RemoveProperty(Guid propertyId)
    {
        var item = _items.FirstOrDefault(i => i.AppraisalPropertyId == propertyId);
        if (item is null)
            throw new InvalidOperationException($"Property {propertyId} is not in this group");

        _items.Remove(item);

        // Resequence remaining items
        for (var i = 0; i < _items.Count; i++) _items[i].UpdateSequence(i + 1);
    }

    /// <summary>
    /// Insert a property at a specific position, shifting existing items down.
    /// Position is clamped to [1, count+1].
    /// </summary>
    public PropertyGroupItem InsertProperty(Guid propertyId, int position)
    {
        if (_items.Any(i => i.AppraisalPropertyId == propertyId))
            throw new InvalidOperationException($"Property {propertyId} is already in this group");

        // Clamp position to valid range
        position = Math.Clamp(position, 1, _items.Count + 1);

        var item = PropertyGroupItem.Create(Id, propertyId, position);

        // Shift items at or after the target position
        foreach (var existing in _items.Where(i => i.SequenceInGroup >= position))
            existing.UpdateSequence(existing.SequenceInGroup + 1);

        _items.Add(item);

        return item;
    }

    /// <summary>
    /// Reorder all properties in this group using the provided ordered list.
    /// The list must contain exactly the same property IDs currently in the group.
    /// </summary>
    public void ReorderProperties(List<Guid> orderedPropertyIds)
    {
        if (orderedPropertyIds.Count != _items.Count)
            throw new InvalidOperationException(
                $"Expected {_items.Count} property IDs but received {orderedPropertyIds.Count}");

        var currentIds = _items.Select(i => i.AppraisalPropertyId).ToHashSet();
        var providedIds = orderedPropertyIds.ToHashSet();

        if (!currentIds.SetEquals(providedIds))
            throw new InvalidOperationException(
                "Provided property IDs do not match the current items in this group");

        for (var i = 0; i < orderedPropertyIds.Count; i++)
        {
            var item = _items.First(x => x.AppraisalPropertyId == orderedPropertyIds[i]);
            item.UpdateSequence(i + 1);
        }
    }

    /// <summary>
    /// Update group details
    /// </summary>
    public void Update(string groupName, string? description, bool useSystemCalc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        GroupName = groupName;
        Description = description;
        UseSystemCalc = useSystemCalc;
    }
}