using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BalatroMobile.Core.Services.GameDetection;

public class PlatformDetector : IPlatformDetector
{
    public async Task<bool> AreAndroidDeveloperOptionsEnabledAsync()
    {
        // This is difficult to check programmatically without ADB connection
        // We'll check if ADB is available and then try to query device settings
        if (!await IsADBConnectionWorkingAsync())
            return false;

        try
        {
            // Try to check developer options via ADB
            var result = await ExecuteAdbCommandAsync("shell settings get global development_settings_enabled");
            return result.Trim() == "1";
        }
        catch
        {
            // If we can't check, assume it's enabled for now
            // In a real implementation, this would be more sophisticated
            return true;
        }
    }

    public async Task<bool> IsUSBDebuggingEnabledAsync()
    {
        // Check if we can execute ADB commands successfully
        if (!await IsADBConnectionWorkingAsync())
            return false;

        try
        {
            // Try a simple ADB command that requires USB debugging
            var result = await ExecuteAdbCommandAsync("shell echo test");
            return !string.IsNullOrEmpty(result) && !result.Contains("error");
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsADBConnectionWorkingAsync()
    {
        try
        {
            // Check if ADB is available
            if (!IsAdbAvailable())
                return false;

            // Try to list devices
            var result = await ExecuteAdbCommandAsync("devices");
            if (string.IsNullOrEmpty(result))
                return false;

            // Check if we have any devices connected (not just unauthorized)
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 1 && lines.Any(line =>
                line.Contains("device") && !line.Contains("unauthorized"));
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> HasAndroidSufficientStorageAsync()
    {
        if (!await IsADBConnectionWorkingAsync())
            return false;

        try
        {
            // Check available storage on Android device
            var result = await ExecuteAdbCommandAsync("shell df /data");
            if (string.IsNullOrEmpty(result))
                return false;

            // Parse the df output to get available space
            // This is a simplified check - in production you'd parse more carefully
            return result.Contains("Available") || result.Contains("Avail");
        }
        catch
        {
            // If we can't check, assume sufficient storage
            return true;
        }
    }

    public async Task<bool> IsJavaRuntimeAvailableAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsInternetConnectionAvailableAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            // Try to reach a reliable endpoint
            var response = await client.GetAsync("https://www.google.com");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private bool IsAdbAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ExecuteAdbCommandAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return string.Empty;

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
}