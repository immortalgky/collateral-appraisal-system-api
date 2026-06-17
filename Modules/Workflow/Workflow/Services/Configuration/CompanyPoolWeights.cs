using Workflow.Services.Configuration.Models;

namespace Workflow.Services.Configuration;

/// <summary>
/// Canonicalises a company round-robin pool's entries so every write path (admin API and the seeder)
/// stores the same shape: no empty company ids, one entry per company, weights as the smallest
/// equivalent positive integers. Reducing by the GCD keeps the weighted-round length proportional
/// (e.g. 100/50 → 2/1, all-100 → all-1) instead of scaling with the raw values.
/// </summary>
public static class CompanyPoolWeights
{
    public static List<CompanyWeightDto> Normalize(IEnumerable<CompanyWeightDto> entries)
    {
        // Drop blanks, clamp to >= 1, and keep the first entry per company (dedup).
        var deduped = new List<CompanyWeightDto>();
        var seen = new HashSet<Guid>();
        foreach (var e in entries)
        {
            if (e.CompanyId == Guid.Empty || !seen.Add(e.CompanyId)) continue;
            deduped.Add(new CompanyWeightDto { CompanyId = e.CompanyId, Weight = e.Weight < 1 ? 1 : e.Weight });
        }

        var gcd = deduped.Aggregate(0, (acc, e) => Gcd(acc, e.Weight));
        if (gcd > 1)
            foreach (var e in deduped) e.Weight /= gcd;

        return deduped;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}
