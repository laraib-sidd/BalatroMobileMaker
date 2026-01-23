namespace BalatroMobile.Core.Services.GameDetection;

public class GameDetector : IGameDetector
{
    public async Task<bool> IsSteamBalatroInstalledAsync()
    {
        var gamePath = await GetGameInstallPathAsync();
        return !string.IsNullOrEmpty(gamePath);
    }

    /// <summary>
    /// Checks for Balatro.exe in the current directory.
    /// User should copy Balatro.exe to the same folder as BalatroMobile.exe.
    /// </summary>
    public async Task<string?> GetGameInstallPathAsync()
    {
        // Check current directory for Balatro.exe
        // User should copy it here from Steam
        var currentDir = Environment.CurrentDirectory;
        var exePath = Path.Combine(currentDir, "Balatro.exe");
        
        if (File.Exists(exePath))
        {
            return currentDir;
        }

        // Also check for Game.love (alternative format some users have)
        var lovePath = Path.Combine(currentDir, "Game.love");
        if (File.Exists(lovePath))
        {
            return currentDir;
        }

        return null;
    }

    /// <summary>
    /// Gets the path to the Balatro executable or Game.love file.
    /// </summary>
    public string? GetBalatroFilePath()
    {
        var currentDir = Environment.CurrentDirectory;
        
        var exePath = Path.Combine(currentDir, "Balatro.exe");
        if (File.Exists(exePath))
        {
            return exePath;
        }

        var lovePath = Path.Combine(currentDir, "Game.love");
        if (File.Exists(lovePath))
        {
            return lovePath;
        }

        return null;
    }

    /// <summary>
    /// Gets the path to the Balatro mods folder.
    /// This is in AppData/Roaming/Balatro/Mods regardless of where the game is.
    /// </summary>
    public string GetModsFolderPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Balatro", 
            "Mods");
    }

    /// <summary>
    /// Checks if the mods folder exists and has content.
    /// </summary>
    public bool HasModsInstalled()
    {
        var modsPath = GetModsFolderPath();
        return Directory.Exists(modsPath) && 
               Directory.GetDirectories(modsPath).Length > 0;
    }

    /// <summary>
    /// Gets the path to the Lovely dump folder.
    /// </summary>
    public string GetLovelyDumpPath()
    {
        return Path.Combine(GetModsFolderPath(), "lovely", "dump");
    }

    /// <summary>
    /// Checks if the Lovely dump exists (required for mods to work).
    /// </summary>
    public bool HasLovelyDump()
    {
        var dumpPath = GetLovelyDumpPath();
        if (!Directory.Exists(dumpPath))
            return false;
            
        var luaFiles = Directory.GetFiles(dumpPath, "*.lua", SearchOption.AllDirectories);
        return luaFiles.Length > 0;
    }
}