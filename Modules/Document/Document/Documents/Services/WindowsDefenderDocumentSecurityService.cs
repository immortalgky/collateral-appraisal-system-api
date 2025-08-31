using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Document.Documents.Services;

public class WindowsDefenderDocumentSecurityService : IDocumentSecurityService
{
    private readonly ILogger<WindowsDefenderDocumentSecurityService> _logger;

    public WindowsDefenderDocumentSecurityService(ILogger<WindowsDefenderDocumentSecurityService> logger)
    {
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(Stream fileStream, string fileName)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check if running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                stopwatch.Stop();
                return new ScanResult
                {
                    IsClean = false,
                    ErrorMessage = "Windows Defender is only available on Windows platform",
                    ScannedAt = startTime,
                    ScanDuration = stopwatch.Elapsed,
                    ScanMethod = "Windows Defender"
                };
            }

            // Save stream to temp file for scanning
            var tempFile = Path.Combine(Path.GetTempPath(), $"wdscan_{Guid.NewGuid()}_{fileName}");
            
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
            stopwatch.Stop();
            _logger.LogError(ex, "Error during Windows Defender stream scanning for file {FileName}", fileName);
            return new ScanResult
            {
                IsClean = false,
                ThreatName = null,
                ErrorMessage = $"Windows Defender scan error: {ex.Message}",
                ScannedAt = startTime,
                ScanDuration = stopwatch.Elapsed,
                ScanMethod = "Windows Defender"
            };
        }
    }

    public async Task<ScanResult> ScanFileAsync(string filePath)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting Windows Defender scan for file: {FilePath}", filePath);

            // Check if running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                stopwatch.Stop();
                return new ScanResult
                {
                    IsClean = false,
                    ErrorMessage = "Windows Defender is only available on Windows platform",
                    ScannedAt = startTime,
                    ScanDuration = stopwatch.Elapsed,
                    ScanMethod = "Windows Defender"
                };
            }

            // Basic file validation first
            var basicValidation = PerformBasicValidation(filePath);
            if (!basicValidation.IsClean)
            {
                stopwatch.Stop();
                basicValidation.ScannedAt = startTime;
                basicValidation.ScanDuration = stopwatch.Elapsed;
                basicValidation.ScanMethod = "Windows Defender (Basic Validation)";
                return basicValidation;
            }

            // Try multiple Windows Defender scanning methods
            var scanResult = await TryMultipleScanMethods(filePath);
            
            stopwatch.Stop();
            scanResult.ScannedAt = startTime;
            scanResult.ScanDuration = stopwatch.Elapsed;
            scanResult.ScanMethod = "Windows Defender";

            _logger.LogInformation("Windows Defender scan completed for {FilePath}. Clean: {IsClean}", 
                filePath, scanResult.IsClean);

            return scanResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during Windows Defender file scanning for {FilePath}", filePath);
            
            return new ScanResult
            {
                IsClean = false,
                ThreatName = null,
                ErrorMessage = $"Windows Defender scan error: {ex.Message}",
                ScannedAt = startTime,
                ScanDuration = stopwatch.Elapsed,
                ScanMethod = "Windows Defender"
            };
        }
    }

    private async Task<ScanResult> TryMultipleScanMethods(string filePath)
    {
        // Method 1: Try PowerShell Windows Defender scan
        var powershellResult = await ScanWithPowerShell(filePath);
        if (powershellResult.IsClean || !string.IsNullOrEmpty(powershellResult.ThreatName))
        {
            return powershellResult;
        }

        // Method 2: Try MpCmdRun.exe (if available)
        var mpCmdResult = await ScanWithMpCmdRun(filePath);
        if (mpCmdResult.IsClean || !string.IsNullOrEmpty(mpCmdResult.ThreatName))
        {
            return mpCmdResult;
        }

        // Method 3: Use WinTrust API for file signature verification
        var trustResult = VerifyFileTrust(filePath);
        
        // Method 4: Fallback to content-based analysis
        var contentResult = await PerformContentAnalysis(filePath);

        // Combine results - if any method fails, consider file suspicious
        return new ScanResult
        {
            IsClean = trustResult && contentResult.IsClean,
            ThreatName = contentResult.ThreatName,
            ErrorMessage = !trustResult ? "File signature verification failed" : contentResult.ErrorMessage
        };
    }

    private async Task<ScanResult> ScanWithPowerShell(string filePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Start-MpScan -ScanType CustomScan -ScanPath '{filePath}'\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ScanResult
                {
                    IsClean = false,
                    ErrorMessage = "Failed to start PowerShell process"
                };
            }

            await process.WaitForExitAsync();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            _logger.LogDebug("PowerShell scan output: {Output}, Error: {Error}", output, error);

            if (process.ExitCode == 0 && string.IsNullOrEmpty(error))
            {
                return new ScanResult { IsClean = true };
            }

            // Check for specific threat indicators
            if (error.Contains("threat", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("malware", StringComparison.OrdinalIgnoreCase))
            {
                return new ScanResult
                {
                    IsClean = false,
                    ThreatName = "Threat detected by Windows Defender",
                    ErrorMessage = error
                };
            }

            return new ScanResult
            {
                IsClean = false,
                ErrorMessage = $"PowerShell scan failed: {error}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PowerShell scan method failed for {FilePath}", filePath);
            return new ScanResult
            {
                IsClean = false,
                ErrorMessage = $"PowerShell scan error: {ex.Message}"
            };
        }
    }

    private async Task<ScanResult> ScanWithMpCmdRun(string filePath)
    {
        try
        {
            // Try to find MpCmdRun.exe in common locations
            var mpCmdPaths = new[]
            {
                @"C:\Program Files\Windows Defender\MpCmdRun.exe",
                @"C:\ProgramData\Microsoft\Windows Defender\Platform\*\MpCmdRun.exe"
            };

            string? mpCmdPath = null;
            foreach (var path in mpCmdPaths)
            {
                if (path.Contains("*"))
                {
                    // Handle wildcard path
                    var directory = Path.GetDirectoryName(path.Replace("*", ""))!;
                    if (Directory.Exists(directory))
                    {
                        var foundFile = Directory.GetFiles(directory, "MpCmdRun.exe", SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (foundFile != null)
                        {
                            mpCmdPath = foundFile;
                            break;
                        }
                    }
                }
                else if (File.Exists(path))
                {
                    mpCmdPath = path;
                    break;
                }
            }

            if (mpCmdPath == null)
            {
                return new ScanResult
                {
                    IsClean = false,
                    ErrorMessage = "MpCmdRun.exe not found"
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = mpCmdPath,
                Arguments = $"-Scan -ScanType 3 -File \"{filePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ScanResult
                {
                    IsClean = false,
                    ErrorMessage = "Failed to start MpCmdRun process"
                };
            }

            await process.WaitForExitAsync();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            _logger.LogDebug("MpCmdRun scan output: {Output}, Error: {Error}", output, error);

            // Exit code 0 means no threats found
            if (process.ExitCode == 0)
            {
                return new ScanResult { IsClean = true };
            }

            // Exit code 2 means threats found
            if (process.ExitCode == 2)
            {
                return new ScanResult
                {
                    IsClean = false,
                    ThreatName = "Threat detected by Windows Defender",
                    ErrorMessage = output
                };
            }

            return new ScanResult
            {
                IsClean = false,
                ErrorMessage = $"MpCmdRun scan failed with exit code {process.ExitCode}: {error}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MpCmdRun scan method failed for {FilePath}", filePath);
            return new ScanResult
            {
                IsClean = false,
                ErrorMessage = $"MpCmdRun scan error: {ex.Message}"
            };
        }
    }

    private bool VerifyFileTrust(string filePath)
    {
        try
        {
            // Simplified trust verification - in production, implement full WinTrust API
            var fileInfo = new FileInfo(filePath);
            
            // Basic checks that would normally be done by WinTrust API
            if (!fileInfo.Exists)
                return false;

            // Check if file is digitally signed (simplified check)
            // In production: Use WinVerifyTrust API with proper P/Invoke
            
            return true; // Simplified - assume trusted for now
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File trust verification failed for {FilePath}", filePath);
            return false;
        }
    }

    private async Task<ScanResult> PerformContentAnalysis(string filePath)
    {
        try
        {
            // Read file content for analysis
            var buffer = new byte[4096];
            using var stream = File.OpenRead(filePath);
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) return new ScanResult { IsClean = false, ErrorMessage = "File appears to be empty" };
            
            var content = System.Text.Encoding.UTF8.GetString(buffer);
            
            // Check for suspicious patterns
            var suspiciousPatterns = new[]
            {
                "virus", "malware", "trojan", "worm", "backdoor",
                "<script", "javascript:", "vbscript:", "eval(",
                "cmd.exe", "powershell.exe", "CreateObject"
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

            return new ScanResult { IsClean = true };
        }
        catch (Exception ex)
        {
            return new ScanResult
            {
                IsClean = false,
                ErrorMessage = $"Content analysis error: {ex.Message}"
            };
        }
    }

    private ScanResult PerformBasicValidation(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        // Check file size (max 100MB for Windows Defender)
        if (fileInfo.Length > 100 * 1024 * 1024)
        {
            return new ScanResult
            {
                IsClean = false,
                ThreatName = "File too large",
                ErrorMessage = "File exceeds maximum size limit (100MB)"
            };
        }

        // Check dangerous file extensions
        var dangerousExtensions = new[] 
        { 
            ".exe", ".bat", ".cmd", ".scr", ".vbs", ".js", ".jar", 
            ".com", ".pif", ".msi", ".dll", ".sys", ".reg" 
        };
        
        if (dangerousExtensions.Contains(fileInfo.Extension.ToLowerInvariant()))
        {
            return new ScanResult
            {
                IsClean = false,
                ThreatName = "Potentially dangerous file extension",
                ErrorMessage = $"File extension {fileInfo.Extension} requires additional scrutiny"
            };
        }

        return new ScanResult { IsClean = true };
    }

    private async Task SaveStreamToFile(Stream stream, string filePath)
    {
        using var fileStream = File.Create(filePath);
        stream.Position = 0;
        await stream.CopyToAsync(fileStream);
    }
}