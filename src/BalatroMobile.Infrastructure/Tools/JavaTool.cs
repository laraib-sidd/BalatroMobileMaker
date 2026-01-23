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

            return process.ExitCode == 0 ? output : error;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var result = await ExecuteAndCaptureOutputAsync("-version");
            return !string.IsNullOrEmpty(result) && !result.Contains("not found");
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