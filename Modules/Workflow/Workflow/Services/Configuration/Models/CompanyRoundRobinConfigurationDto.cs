namespace Workflow.Services.Configuration.Models;

/// <summary>A single company in the round-robin pool, with its relative weight.</summary>
public class CompanyWeightDto
{
    public Guid CompanyId { get; set; }
    public int Weight { get; set; } = 1;
}

/// <summary>Read model for a company round-robin pool configuration.</summary>
public class CompanyRoundRobinConfigurationDto
{
    public Guid Id { get; set; }
    public string? LoanType { get; set; }
    public List<CompanyWeightDto> Entries { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = default!;
    public string UpdatedBy { get; set; } = default!;
}

/// <summary>Request model for creating a company round-robin pool configuration.</summary>
public class CreateCompanyRoundRobinConfigurationRequest
{
    public string? LoanType { get; set; }
    public List<CompanyWeightDto> Entries { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = default!;
}

/// <summary>Request model for updating a company round-robin pool configuration.</summary>
public class UpdateCompanyRoundRobinConfigurationRequest
{
    public string? LoanType { get; set; }
    public List<CompanyWeightDto> Entries { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public string UpdatedBy { get; set; } = default!;
}
