using Shared.DDD;

namespace Auth.Domain.Groups;

public class Group : Entity<Guid>
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Scope { get; private set; } = default!;
    public Guid? CompanyId { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedOn { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public List<GroupUser> Users { get; private set; } = [];
    public List<GroupMonitoring> MonitoredGroups { get; private set; } = [];

    private Group() { }

    public static Group Create(string name, string description, string scope, Guid? companyId = null)
    {
        return new Group
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = description,
            Scope = scope,
            CompanyId = companyId,
            IsDeleted = false
        };
    }

    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public void Delete(Guid? deletedBy)
    {
        IsDeleted = true;
        DeletedOn = DateTime.Now;
        DeletedBy = deletedBy;
    }
}
