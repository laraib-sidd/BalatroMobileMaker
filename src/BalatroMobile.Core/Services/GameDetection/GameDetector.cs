using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BalatroMobile.Core.Services.GameDetection;

public class GameDetector : IGameDetector
{
    // Allow external override of game path (set by CLI when user provides path)
    public static string? OverrideGamePath { get; set; }

    public async Task<bool> IsSteamBalatroInstalledAsync()
    {
        var gamePath = await GetGameInstallPathAsync();
        return !string.IsNullOrEmpty(gamePath) && File.Exists(Path.Combine(gamePath, "Balatro.exe"));
    }

    public async Task<string?> GetGameInstallPathAsync()
    {
        // Check for manual override first (set by CLI when user provides path)
        if (!string.IsNullOrEmpty(OverrideGamePath) && Directory.Exists(OverrideGamePath))
        {
            var overrideExePath = Path.Combine(OverrideGamePath, "Balatro.exe");
            if (File.Exists(overrideExePath))
            {
                return OverrideGamePath;
            }
        }

        // Build list of possible paths
        var possiblePaths = new List<string>();

        // 1. Check environment variable override
        var envPath = Environment.GetEnvironmentVariable("STEAM_GAME_PATH_BALATRO");
        if (!string.IsNullOrEmpty(envPath))
        {
            possiblePaths.Add(envPath);
        }

        // 2. Check current directory
        possiblePaths.Add(Directory.GetCurrentDirectory());

        // 3. Discover all Steam library folders
        var steamLibraries = await DiscoverSteamLibrariesAsync();
        foreach (var library in steamLibraries)
        {
            possiblePaths.Add(Path.Combine(library, "steamapps", "common", "Balatro"));
        }

        // 4. Default Steam locations as fallback
        possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Balatro"));
        possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Balatro"));

        // 5. Check common alternative locations
        var driveLetters = new[] { "C", "D", "E", "F", "G" };
        foreach (var drive in driveLetters)
        {
            possiblePaths.Add($@"{drive}:\Steam\steamapps\common\Balatro");
            possiblePaths.Add($@"{drive}:\SteamLibrary\steamapps\common\Balatro");
            possiblePaths.Add($@"{drive}:\Games\Steam\steamapps\common\Balatro");
            possiblePaths.Add($@"{drive}:\Games\SteamLibrary\steamapps\common\Balatro");
        }

        // Check each path
        foreach (var path in possiblePaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            try
            {
                // If it's a direct exe path, get the directory
                var checkPath = path.EndsWith("Balatro.exe", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(path)
                    : path;

                if (!string.IsNullOrEmpty(checkPath) && Directory.Exists(checkPath))
                {
                    var exePath = Path.Combine(checkPath, "Balatro.exe");
                    if (File.Exists(exePath))
                    {
                        return checkPath;
                    }
                }
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        return null;
    }

    /// <summary>
    /// Discovers all Steam library folders by parsing libraryfolders.vdf
    /// </summary>
    private async Task<List<string>> DiscoverSteamLibrariesAsync()
    {
        var libraries = new List<string>();

        try
        {
            // Find Steam installation
            var steamPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
                @"C:\Steam",
                @"D:\Steam"
            };

            string? steamPath = null;
            foreach (var path in steamPaths)
            {
                if (Directory.Exists(path))
                {
                    steamPath = path;
                    break;
                }
            }

            if (steamPath == null)
                return libraries;

            // Add the main Steam path
            libraries.Add(steamPath);

            // Parse libraryfolders.vdf to find additional libraries
            var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFoldersPath))
            {
                var content = await File.ReadAllTextAsync(libraryFoldersPath);

                // Parse VDF format to extract paths
                // Look for "path" entries like: "path"		"D:\\SteamLibrary"
                var pathRegex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                var matches = pathRegex.Matches(content);

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var libraryPath = match.Groups[1].Value.Replace(@"\\", @"\");
                        if (Directory.Exists(libraryPath) && !libraries.Contains(libraryPath))
                        {
                            libraries.Add(libraryPath);
                        }
                    }
                }
            }
        }
        catch
        {
            // If we fail to parse, just return what we have
        }

        return libraries;
    }
}