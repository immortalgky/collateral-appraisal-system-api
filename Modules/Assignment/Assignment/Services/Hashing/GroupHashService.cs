using System.Security.Cryptography;
using System.Text;

namespace Assignment.Services.Hashing;

public class GroupHashService : IGroupHashService
{
    public string GenerateGroupsHash(List<string> userGroups)
    {
        if (userGroups == null || !userGroups.Any())
        {
            return GenerateHash(string.Empty);
        }

        var sortedGroups = userGroups
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groupsString = string.Join(",", sortedGroups);
        return GenerateHash(groupsString);
    }

    public string GenerateGroupsList(List<string> userGroups)
    {
        if (userGroups == null || !userGroups.Any())
        {
            return string.Empty;
        }

        var sortedGroups = userGroups
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(",", sortedGroups);
    }

    private static string GenerateHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)[..8]; // Take the first 8 characters for shorter hash
    }
}