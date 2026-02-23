using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;

namespace Appraisal.Infrastructure.Seed;

public class AppendixTypeDataSeed : IDataSeeder<AppraisalDbContext>
{
    private readonly AppraisalDbContext _context;
    private readonly ILogger<AppendixTypeDataSeed> _logger;

    public AppendixTypeDataSeed(
        AppraisalDbContext context,
        ILogger<AppendixTypeDataSeed> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAllAsync()
    {
        if (await _context.AppendixTypes.AnyAsync())
        {
            _logger.LogInformation("Appendix types already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding appendix types...");

        var types = new List<AppendixType>
        {
            AppendixType.Create("BRIEF_MAP", "Brief Map", 0),
            AppendixType.Create("DETAILED_MAP", "Detailed Map", 1),
            AppendixType.Create("EARTH_MAP", "Earth Map", 2),
            AppendixType.Create("LAND_MAP", "Land Map", 3),
            AppendixType.Create("CITY_PLAN", "City Plan", 4),
            AppendixType.Create("STATUTORY_PLAN", "Statutory Plan", 5),
            AppendixType.Create("LAND_PLAN", "Land Plan", 6),
            AppendixType.Create("BUILDING_LAYOUT", "Building Layout", 7),
            AppendixType.Create("BLUEPRINT", "Blueprint", 8),
            AppendixType.Create("PHOTO_SPOT", "Photo and Photo Spot", 9),
            AppendixType.Create("REG_INDEX", "Registration Index", 10)
        };

        _context.AppendixTypes.AddRange(types);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} appendix types", types.Count);
    }
}
