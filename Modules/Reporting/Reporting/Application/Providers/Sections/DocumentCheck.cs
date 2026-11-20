namespace Reporting.Application.Providers.Sections;

/// <summary>The document check result (parameter group DocumentValidation): only the two codes it defines count; anything else is "no result".</summary>
internal static class DocumentCheck
{
    public const string Match = "01";      // ถูกต้องตรงกัน
    public const string Mismatch = "02";   // ไม่สอดคล้องกัน

    public static string? Result(string? code) => code?.Trim() is Match or Mismatch ? code.Trim() : null;
}
