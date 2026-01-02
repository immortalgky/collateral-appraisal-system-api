using System.Text.RegularExpressions;

namespace Database.Migration;

/// <summary>
/// Utility for extracting version information from migration script names
/// </summary>
public static class VersionExtractor
{
    /// <summary>
    /// Extracts version from script name using various patterns
    /// </summary>
    /// <param name="scriptName">The script file name</param>
    /// <returns>Version string or auto-generated version based on timestamp</returns>
    public static string ExtractVersion(string scriptName)
    {
        if (string.IsNullOrEmpty(scriptName))
            return "1.0.0";

        // Pattern 1: v1.2.3 or V1.2.3 prefix
        var versionMatch = Regex.Match(scriptName, @"[vV](\d+\.\d+\.\d+)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        if (versionMatch.Success)
        {
            return versionMatch.Groups[1].Value;
        }

        // Pattern 2: 001_, 002_, etc. prefix - convert to semantic version
        var numberMatch = Regex.Match(scriptName, @"^(\d{3,})_", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (numberMatch.Success)
        {
            var number = int.Parse(numberMatch.Groups[1].Value);
            var major = number / 100;
            var minor = (number % 100) / 10;
            var patch = number % 10;
            return $"{major}.{minor}.{patch}";
        }

        // Pattern 3: Extract from date-based migration names (yyyyMMddHHmmss)
        var dateMatch = Regex.Match(scriptName, @"(\d{14})", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (dateMatch.Success)
        {
            var dateStr = dateMatch.Groups[1].Value;
            if (DateTime.TryParseExact(dateStr, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                // Convert to version: year.month.day format
                return $"{date.Year}.{date.Month:D2}.{date.Day:D2}";
            }
        }

        // Pattern 4: Default versioning based on script type and order
        return GenerateDefaultVersion(scriptName);
    }

    /// <summary>
    /// Generates a default version based on script name and type
    /// </summary>
    private static string GenerateDefaultVersion(string scriptName)
    {
        var lowerName = scriptName.ToLowerInvariant();
        
        // Base version on script type
        if (lowerName.Contains("view"))
        {
            return "1.1.0"; // Views start at 1.1.x
        }
        if (lowerName.Contains("procedure") || lowerName.Contains("proc"))
        {
            return "1.2.0"; // Stored procedures start at 1.2.x
        }
        if (lowerName.Contains("function") || lowerName.Contains("func"))
        {
            return "1.3.0"; // Functions start at 1.3.x
        }
        
        // Default for database creation scripts
        if (lowerName.Contains("create") || lowerName.Contains("initial"))
        {
            return "1.0.0";
        }

        // Fallback
        return "1.0.1";
    }

    /// <summary>
    /// Compares two version strings
    /// </summary>
    /// <param name="version1">First version</param>
    /// <param name="version2">Second version</param>
    /// <returns>-1 if version1 < version2, 0 if equal, 1 if version1 > version2</returns>
    public static int CompareVersions(string version1, string version2)
    {
        if (string.IsNullOrEmpty(version1)) version1 = "0.0.0";
        if (string.IsNullOrEmpty(version2)) version2 = "0.0.0";

        var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
        var v2Parts = version2.Split('.').Select(int.Parse).ToArray();

        // Ensure both have 3 parts
        Array.Resize(ref v1Parts, 3);
        Array.Resize(ref v2Parts, 3);

        for (int i = 0; i < 3; i++)
        {
            if (v1Parts[i] < v2Parts[i]) return -1;
            if (v1Parts[i] > v2Parts[i]) return 1;
        }

        return 0;
    }

    /// <summary>
    /// Checks if a version is greater than or equal to target version
    /// </summary>
    public static bool IsVersionGreaterOrEqual(string currentVersion, string targetVersion)
    {
        return CompareVersions(currentVersion, targetVersion) >= 0;
    }
}