using System.Diagnostics;

namespace Document.Documents.Services;

public class ClamAvDocumentSecurityService : IDocumentSecurityService
{
    private readonly ILogger<ClamAvDocumentSecurityService> _logger;

    public ClamAvDocumentSecurityService(ILogger<ClamAvDocumentSecurityService> logger)
    {
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(Stream fileStream, string fileName)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Save stream to temp file for scanning
            var tempFile = Path.Combine(Path.GetTempPath(), $"scan_{Guid.NewGuid()}_{fileName}");
            
            try
            {
                await SaveStreamToFile(fileStream, tempFile);
                var result = await ScanFileAsync(tempFile);
                return result;
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ClamAV stream scanning for file {FileName}", fileName);
            return new ScanResult
            {
                IsClean = false,
                ThreatName = null,
                ErrorMessage = $"Scan error: {ex.Message}",
                ScannedAt = startTime,
                ScanDuration = stopwatch.Elapsed,
                ScanMethod = "ClamAV"
            };
        }
    }

    public async Task<ScanResult> ScanFileAsync(string filePath)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting ClamAV scan for file: {FilePath}", filePath);

            // Basic file validation first
            var basicValidation = PerformBasicValidation(filePath);
            if (!basicValidation.IsClean)
            {
                stopwatch.Stop();
                basicValidation.ScannedAt = startTime;
                basicValidation.ScanDuration = stopwatch.Elapsed;
                basicValidation.ScanMethod = "ClamAV (Basic Validation)";
                return basicValidation;
            }

            // For production: Use actual ClamAV integration
            // Install-Package nClam for real implementation
            // var clam = new ClamClient("localhost", 3310);
            // var result = await clam.SendAndScanFileAsync(filePath);
            
            // Mock implementation for demonstration
            await Task.Delay(100); // Simulate scan time
            
            var fileInfo = new FileInfo(filePath);
            
            // Simulate virus detection based on file content patterns
            var scanResult = await SimulateClamAvScan(filePath);
            
            stopwatch.Stop();
            _logger.LogInformation("ClamAV scan completed for {FilePath}. Clean: {IsClean}", 
                filePath, scanResult.IsClean);

            return new ScanResult
            {
                IsClean = scanResult.IsClean,
                ThreatName = scanResult.ThreatName,
                ScannedAt = startTime,
                ScanDuration = stopwatch.Elapsed,
                ScanMethod = "ClamAV Mock",
                ErrorMessage = scanResult.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during ClamAV file scanning for {FilePath}", filePath);
            
            return new ScanResult
            {
                IsClean = false,
                ThreatName = null,
                ErrorMessage = $"ClamAV scan error: {ex.Message}",
                ScannedAt = startTime,
                ScanDuration = stopwatch.Elapsed,
                ScanMethod = "ClamAV"
            };
        }
    }

    private ScanResult PerformBasicValidation(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        // Check file size (max 50MB)
        if (fileInfo.Length > 50 * 1024 * 1024)
        {
            return new ScanResult
            {
                IsClean = false,
                ThreatName = "File too large",
                ErrorMessage = "File exceeds maximum size limit (50MB)"
            };
        }

        // Check dangerous file extensions
        var dangerousExtensions = new[] { ".exe", ".bat", ".cmd", ".scr", ".vbs", ".js", ".jar", ".com" };
        if (dangerousExtensions.Contains(fileInfo.Extension.ToLowerInvariant()))
        {
            return new ScanResult
            {
                IsClean = false,
                ThreatName = "Dangerous file extension",
                ErrorMessage = $"File extension {fileInfo.Extension} is not allowed"
            };
        }

        return new ScanResult { IsClean = true };
    }

    private async Task<ScanResult> SimulateClamAvScan(string filePath)
    {
        // Read first few bytes to simulate content analysis
        var buffer = new byte[1024];
        using var stream = File.OpenRead(filePath);
        var bytesToRead = Math.Min(buffer.Length, (int)stream.Length);
        if (bytesToRead == 0) return new ScanResult { IsClean = false, ThreatName = "Empty file", ErrorMessage = "File appears to be empty" };
        var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);
        if (bytesRead == 0) return new ScanResult { IsClean = false, ThreatName = "Empty file", ErrorMessage = "Could not read file content" };
        
        // Check for suspicious patterns (mock implementation)
        var content = System.Text.Encoding.UTF8.GetString(buffer);
        
        // Simulate virus signature detection
        var suspiciousPatterns = new[]
        {
            "virus", "malware", "trojan", "worm", "rootkit",
            "<script", "javascript:", "vbscript:", "eval("
        };

        foreach (var pattern in suspiciousPatterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return new ScanResult
                {
                    IsClean = false,
                    ThreatName = $"Suspicious pattern detected: {pattern}",
                    ErrorMessage = "File contains potentially malicious content"
                };
            }
        }

        // Simulate PDF-specific checks
        if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await ValidatePdfContent(filePath);
        }

        return new ScanResult { IsClean = true };
    }

    private async Task<ScanResult> ValidatePdfContent(string filePath)
    {
        try
        {
            // Basic PDF structure validation
            var buffer = new byte[1024];
            using var stream = File.OpenRead(filePath);
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) return new ScanResult { IsClean = false, ThreatName = "Empty file", ErrorMessage = "File appears to be empty" };
            
            var header = System.Text.Encoding.ASCII.GetString(buffer, 0, 8);
            
            // Check PDF magic number
            if (!header.StartsWith("%PDF-"))
            {
                return new ScanResult
                {
                    IsClean = false,
                    ThreatName = "Invalid PDF format",
                    ErrorMessage = "File does not appear to be a valid PDF"
                };
            }

            // Check for embedded JavaScript (potential security risk)
            var content = System.Text.Encoding.UTF8.GetString(buffer);
            if (content.Contains("/JavaScript", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("/JS", StringComparison.OrdinalIgnoreCase))
            {
                return new ScanResult
                {
                    IsClean = false,
                    ThreatName = "Embedded JavaScript detected",
                    ErrorMessage = "PDF contains potentially malicious JavaScript"
                };
            }

            return new ScanResult { IsClean = true };
        }
        catch (Exception ex)
        {
            return new ScanResult
            {
                IsClean = false,
                ThreatName = "PDF validation error",
                ErrorMessage = $"Error validating PDF: {ex.Message}"
            };
        }
    }

    private async Task SaveStreamToFile(Stream stream, string filePath)
    {
        using var fileStream = File.Create(filePath);
        stream.Position = 0;
        await stream.CopyToAsync(fileStream);
    }
}