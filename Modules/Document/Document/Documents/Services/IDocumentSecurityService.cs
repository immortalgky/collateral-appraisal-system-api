namespace Document.Documents.Services;

public interface IDocumentSecurityService
{
    Task<ScanResult> ScanAsync(Stream fileStream, string fileName);
    Task<ScanResult> ScanFileAsync(string filePath);
}

public class ScanResult
{
    public bool IsClean { get; set; }
    public string? ThreatName { get; set; }
    public DateTime ScannedAt { get; set; }
    public string ScanMethod { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public TimeSpan ScanDuration { get; set; }
}

public enum ScannerType
{
    ClamAV,
    WindowsDefender,
    Mock
}