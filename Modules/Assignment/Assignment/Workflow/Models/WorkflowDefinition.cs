using Shared.DDD;

namespace Assignment.Workflow.Models;

public class WorkflowDefinition : Entity<Guid>
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public string JsonDefinition { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public new DateTime CreatedOn { get; private set; }
    public new string CreatedBy { get; private set; } = default!;
    public new DateTime? UpdatedOn { get; private set; }
    public new string? UpdatedBy { get; private set; }

    private WorkflowDefinition() { }

    public static WorkflowDefinition Create(
        string name,
        string description,
        string jsonDefinition,
        string category,
        string createdBy)
    {
        return new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Version = 1,
            IsActive = true,
            JsonDefinition = jsonDefinition,
            Category = category,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    public void UpdateDefinition(
        string name,
        string description,
        string jsonDefinition,
        string category,
        string updatedBy)
    {
        Name = name;
        Description = description;
        JsonDefinition = jsonDefinition;
        Category = category;
        UpdatedOn = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        Version++;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}