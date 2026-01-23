using System.Diagnostics;

namespace BalatroMobile.Infrastructure.Tools;

public class JavaTool : IJavaTool
{
    private readonly string _javaExecutablePath;

    public JavaTool(string javaExecutablePath = "java")
    {
        _javaExecutablePath = javaExecutablePath;
    }

    public async Task<bool> ExecuteAsync(string arguments, string? workingDirectory = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _javaExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ExecuteAndCaptureOutputAsync(string arguments, string? workingDirectory = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _javaExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return string.Empty;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            // IMPORTANT: Java outputs version info to stderr, not stdout!
            // Always combine both streams to capture all output
            var combined = output + error;
            
            // If exit code is non-zero, prefix with error indicator
            if (process.ExitCode != 0 && !combined.Contains("Error"))
            {
                return $"[ExitCode:{process.ExitCode}] {combined}";
            }
            
            return combined;
        }
        catch (Exception ex)
        {
            return $"[Exception] {ex.Message}";
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Check if the executable file exists (for bundled Java)
            if (!string.IsNullOrEmpty(_javaExecutablePath) && 
                _javaExecutablePath != "java" && 
                !File.Exists(_javaExecutablePath))
            {
                return false;
            }

            var result = await ExecuteAndCaptureOutputAsync("-version");

            // Java is available if we got output containing version info
            // and no error indicators
            return !string.IsNullOrEmpty(result) && 
                   !result.Contains("[Exception]") &&
                   !result.Contains("not found") &&
                   !result.Contains("not recognized") &&
                   (result.Contains("version") || result.Contains("openjdk") || result.Contains("java"));
        }
        catch
        {
            return false;
        }
    }

    public string GetVersion()
    {
        // This would need to be implemented to parse version from java -version output
        return "Unknown";
    }
}