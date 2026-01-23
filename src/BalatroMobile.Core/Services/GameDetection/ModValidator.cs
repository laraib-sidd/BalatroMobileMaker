namespace BalatroMobile.Core.Services.GameDetection;

public class ModValidator : IModValidator
{
    private readonly string _modsPath;

    public ModValidator()
    {
        // Balatro mods are typically installed in %APPDATA%\Balatro\Mods
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _modsPath = Path.Combine(appData, "Balatro", "Mods");
    }

    public async Task<bool> ValidateModsFolderStructureAsync()
    {
        if (!Directory.Exists(_modsPath))
            return false;

        // Check for essential mod directories
        var requiredMods = new[] { "lovely", "smods", "Steamodded" };
        var existingMods = Directory.GetDirectories(_modsPath)
            .Select(dir => Path.GetFileName(dir).ToLower())
            .ToHashSet();

        // At minimum, we need some form of mod loader
        return existingMods.Contains("lovely") ||
               existingMods.Contains("smods") ||
               existingMods.Any(name => name.Contains("steamodded"));
    }

    public async Task<bool> IsLovelyInjectorWorkingAsync()
    {
        try
        {
            var gamePath = await GetGamePathAsync();
            if (string.IsNullOrEmpty(gamePath))
                return false;

            // Check for version.dll in the game directory (Lovely injector)
            var versionDllPath = Path.Combine(gamePath, "version.dll");
            if (!File.Exists(versionDllPath))
                return false;

            // Verify it's a valid DLL
            var fileInfo = new FileInfo(versionDllPath);
            return fileInfo.Length > 0; // Basic size check
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DoesLovelyDumpExistAsync()
    {
        var dumpPath = Path.Combine(_modsPath, "lovely", "dump");
        if (!Directory.Exists(dumpPath))
            return false;

        // Check if dump directory has content (should have multiple .lua files)
        var luaFiles = Directory.GetFiles(dumpPath, "*.lua", SearchOption.AllDirectories);
        return luaFiles.Length > 10; // Should have many Lua files if dump was generated
    }

    public async Task<bool> AreModsWorkingOnPCAsync()
    {
        // This is harder to check definitively without running the game
        // We'll do some basic checks:
        // 1. Mods folder exists and has content
        // 2. At least one major mod is present
        // 3. Dump exists (indicating mods were loaded)

        if (!await ValidateModsFolderStructureAsync())
            return false;

        if (!await DoesLovelyDumpExistAsync())
            return false;

        // Check for presence of popular mods
        var modsDir = _modsPath;
        var hasCryptid = Directory.Exists(Path.Combine(modsDir, "Cryptid"));
        var hasTalisman = Directory.Exists(Path.Combine(modsDir, "Talisman"));
        var hasTagManager = Directory.Exists(Path.Combine(modsDir, "TagManager"));

        // If we have at least one content mod, assume mods are working
        return hasCryptid || hasTalisman || hasTagManager;
    }

    private async Task<string?> GetGamePathAsync()
    {
        var gameDetector = new GameDetector();
        return await gameDetector.GetGameInstallPathAsync();
    }
}