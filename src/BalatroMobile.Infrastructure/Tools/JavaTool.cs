using System.Diagnostics;
using System.Text.Json;

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
        // #region agent log
        var debugLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BalatroMobile", "debug.log");
        Directory.CreateDirectory(Path.GetDirectoryName(debugLogPath)!);
        void Log(string hyp, string msg, object? data = null) { try { File.AppendAllText(debugLogPath, System.Text.Json.JsonSerializer.Serialize(new { hypothesisId = hyp, location = "JavaTool.cs:IsAvailableAsync", message = msg, data, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { } }
        // #endregion

        try
        {
            // #region agent log
            Log("A", "Java executable path", new { javaPath = _javaExecutablePath, exists = File.Exists(_javaExecutablePath) });
            // #endregion

            var result = await ExecuteAndCaptureOutputAsync("-version");

            // #region agent log
            Log("B", "Java -version result", new { resultLength = result?.Length ?? 0, resultPreview = result?.Substring(0, Math.Min(200, result?.Length ?? 0)), containsNotFound = result?.Contains("not found") ?? false });
            // #endregion

            var isAvailable = !string.IsNullOrEmpty(result) && !result.Contains("not found");

            // #region agent log
            Log("D", "Java availability decision", new { isAvailable, resultEmpty = string.IsNullOrEmpty(result) });
            // #endregion

            return isAvailable;
        }
        catch (Exception ex)
        {
            // #region agent log
            Log("B", "Java check exception", new { exceptionType = ex.GetType().Name, exceptionMessage = ex.Message });
            // #endregion
            return false;
        }
    }

    public string GetVersion()
    {
        // This would need to be implemented to parse version from java -version output
        return "Unknown";
    }
}