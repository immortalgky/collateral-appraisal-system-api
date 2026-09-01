namespace Appraisal.Application.Services;

/// <summary>Issues the next block-project unit ticket number.</summary>
public interface IUnitTicketNumberGenerator
{
    Task<string> GenerateAsync(int thaiYear, CancellationToken cancellationToken = default);
}
