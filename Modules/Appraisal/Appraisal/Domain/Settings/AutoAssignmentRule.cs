namespace Appraisal.Domain.Settings;

/// <summary>
/// Auto-assignment rule for automatically assigning appraisals.
/// Rules are evaluated in priority order (first match wins).
/// </summary>
public class AutoAssignmentRule : Entity<Guid>
{
    public string RuleName { get; private set; } = null!;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Conditions (JSON arrays, null = all)
    public string? PropertyTypes { get; private set; } // JSON array: ["Land", "Building"]
    public string? Provinces { get; private set; } // JSON array: ["Bangkok", "Chonburi"]
    public decimal? MinEstimatedValue { get; private set; }
    public decimal? MaxEstimatedValue { get; private set; }
    public string? LoanTypes { get; private set; } // JSON array
    public string? Priorities { get; private set; } // JSON array: ["Normal", "High"]

    // Action
    public string AssignmentMode { get; private set; } = null!; // Internal, ExternalPanel, ExternalQuotation
    public Guid? AssignToUserId { get; private set; }
    public Guid? AssignToTeamId { get; private set; }
    public Guid? AssignToCompanyId { get; private set; }

    private AutoAssignmentRule()
    {
    }

    public static AutoAssignmentRule Create(
        string ruleName,
        int priority,
        string assignmentMode)
    {
        return new AutoAssignmentRule
        {
            Id = Guid.NewGuid(),
            RuleName = ruleName,
            Priority = priority,
            AssignmentMode = assignmentMode,
            IsActive = true
        };
    }

    public void SetConditions(
        string? propertyTypes,
        string? provinces,
        decimal? minValue,
        decimal? maxValue,
        string? loanTypes,
        string? priorities)
    {
        PropertyTypes = propertyTypes;
        Provinces = provinces;
        MinEstimatedValue = minValue;
        MaxEstimatedValue = maxValue;
        LoanTypes = loanTypes;
        Priorities = priorities;
    }

    public void SetAssignment(Guid? userId, Guid? teamId, Guid? companyId)
    {
        AssignToUserId = userId;
        AssignToTeamId = teamId;
        AssignToCompanyId = companyId;
    }

    public void SetPriority(int priority)
    {
        Priority = priority;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}