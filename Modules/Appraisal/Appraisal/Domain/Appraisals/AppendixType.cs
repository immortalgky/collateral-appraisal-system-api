namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Configuration entity for appendix types (seeded).
/// Defines the predefined list of appendix categories available per appraisal.
/// </summary>
public class AppendixType : Entity<Guid>
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private AppendixType()
    {
    }

    public static AppendixType Create(
        string code,
        string name,
        int sortOrder,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new AppendixType
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Name = name,
            SortOrder = sortOrder,
            Description = description,
            IsActive = true
        };
    }
}
