using Shared.DDD;

namespace Workflow.Sla.Models;

public class Holiday : Entity<Guid>
{
    public DateOnly Date { get; private set; }
    public string Description { get; private set; } = default!;
    public int Year { get; private set; }

    private Holiday() { }

    public static Holiday Create(DateOnly date, string description)
    {
        return new Holiday
        {
            Id = Guid.CreateVersion7(),
            Date = date,
            Description = description,
            Year = date.Year
        };
    }

    public void Update(DateOnly date, string description)
    {
        Date = date;
        Description = description;
        Year = date.Year;
    }
}
