using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BalatroMobile.Core.Services.GameDetection;

public class GameDetector : IGameDetector
{
    public async Task<bool> IsSteamBalatroInstalledAsync()
    {
        var gamePath = await GetGameInstallPathAsync();
        return !string.IsNullOrEmpty(gamePath) && File.Exists(Path.Combine(gamePath, "Balatro.exe"));
    }

    public async Task<string?> GetGameInstallPathAsync()
    {
        // Try common Steam installation paths
        var possiblePaths = new[]
        {
            // Default Steam location
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Balatro"),

            // Alternative Steam locations
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Balatro"),

            // Check environment variables
            Environment.GetEnvironmentVariable("STEAM_GAME_PATH_BALATRO"),

            // Check for Balatro.exe in current directory
            Path.Combine(Directory.GetCurrentDirectory(), "Balatro.exe")
        };

        foreach (var path in possiblePaths)
        {
            if (!string.IsNullOrEmpty(path))
            {
                // If it's a direct exe path, get the directory
                var checkPath = path.EndsWith("Balatro.exe") ? Path.GetDirectoryName(path) : path;

                if (!string.IsNullOrEmpty(checkPath) && Directory.Exists(checkPath))
                {
                    var exePath = Path.Combine(checkPath, "Balatro.exe");
                    if (File.Exists(exePath))
                    {
                        return checkPath;
                    }
                }
            }
        }

        return null;
    }

    public async Task<bool> IsGameWorkingAsync()
    {
        try
        {
            var gamePath = await GetGameInstallPathAsync();
            if (string.IsNullOrEmpty(gamePath))
                return false;

            var exePath = Path.Combine(gamePath, "Balatro.exe");
            if (!File.Exists(exePath))
                return false;

            // Try to get file version info to verify it's a valid executable
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            return versionInfo.ProductName?.Contains("Balatro", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }
}