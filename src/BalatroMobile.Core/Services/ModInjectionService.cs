using BalatroMobile.Core.Models;

namespace BalatroMobile.Core.Services;

public class ModInjectionService : IModInjectionService
{
    public async Task<ModInjectionResult> InjectModsAsync(ModInjectionConfig config)
    {
        var injectedComponents = new List<string>();
        var errors = new List<string>();
        var totalFiles = 0;
        var totalBytes = 0L;

        try
        {
            // Ensure target directory exists
            Directory.CreateDirectory(config.TargetModsPath);

            // 1. Inject Lovely dump files
            if (config.IncludeLovelyDump)
            {
                var dumpResult = await InjectLovelyDumpAsync(config);
                if (dumpResult.Success)
                {
                    injectedComponents.Add("Lovely Dump");
                    totalFiles += dumpResult.FilesCopied;
                    totalBytes += dumpResult.BytesCopied;
                }
                else
                {
                    errors.Add($"Lovely dump injection failed: {string.Join(", ", dumpResult.Errors)}");
                }
            }

            // 2. Inject SMODS framework
            if (config.IncludeSMODS)
            {
                var smodsResult = await InjectSMODSAsync(config);
                if (smodsResult.Success)
                {
                    injectedComponents.Add("SMODS Framework");
                    totalFiles += smodsResult.FilesCopied;
                    totalBytes += smodsResult.BytesCopied;
                }
                else
                {
                    errors.Add($"SMODS injection failed: {string.Join(", ", smodsResult.Errors)}");
                }
            }

            // 3. Inject required libraries
            if (config.IncludeLibraries)
            {
                var libsResult = await InjectLibrariesAsync(config);
                if (libsResult.Success)
                {
                    injectedComponents.Add("Libraries (nativefs, json)");
                    totalFiles += libsResult.FilesCopied;
                    totalBytes += libsResult.BytesCopied;
                }
                else
                {
                    errors.Add($"Library injection failed: {string.Join(", ", libsResult.Errors)}");
                }
            }

            // 4. Create Lovely configuration
            if (config.CreateLovelyConfig)
            {
                var configResult = await CreateLovelyConfigAsync(config);
                if (configResult.Success)
                {
                    injectedComponents.Add("Lovely Configuration");
                }
                else
                {
                    errors.Add($"Lovely config creation failed: {string.Join(", ", configResult.Errors)}");
                }
            }

            // 5. Inject individual mods (excluding specified ones)
            var modsResult = await InjectIndividualModsAsync(config);
            if (modsResult.Success)
            {
                injectedComponents.AddRange(modsResult.InjectedComponents);
                totalFiles += modsResult.FilesCopied;
                totalBytes += modsResult.BytesCopied;
            }
            else
            {
                errors.AddRange(modsResult.Errors);
            }

            return new ModInjectionResult
            {
                Success = !errors.Any(),
                TargetPath = config.TargetModsPath,
                InjectedComponents = injectedComponents,
                Errors = errors,
                FilesCopied = totalFiles,
                BytesCopied = totalBytes
            };

        }
        catch (Exception ex)
        {
            return new ModInjectionResult
            {
                Success = false,
                TargetPath = config.TargetModsPath,
                Errors = new[] { $"Mod injection failed: {ex.Message}" },
                FilesCopied = totalFiles,
                BytesCopied = totalBytes
            };
        }
    }

    public async Task<bool> ValidateModInjectionAsync(ModInjectionConfig config)
    {
        // Check if source paths exist
        if (!Directory.Exists(config.SourceModsPath))
            return false;

        // Check for essential components
        var lovelyDumpPath = Path.Combine(config.SourceModsPath, "lovely", "dump");
        if (config.IncludeLovelyDump && !Directory.Exists(lovelyDumpPath))
            return false;

        var smodsPath = Path.Combine(config.SourceModsPath, "smods");
        if (config.IncludeSMODS && !Directory.Exists(smodsPath))
            return false;

        return true;
    }

    public async Task<IEnumerable<string>> GetAvailableModsAsync()
    {
        var modsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Balatro", "Mods");

        if (!Directory.Exists(modsPath))
            return Array.Empty<string>();

        return Directory.GetDirectories(modsPath)
            .Select(Path.GetFileName)
            .Where(name => name != null && !name.StartsWith("."))
            .Cast<string>()
            .ToArray();
    }

    public async Task<Dictionary<string, bool>> CheckModCompatibilityAsync(IEnumerable<string> modNames)
    {
        // Basic compatibility check - can be expanded
        var results = new Dictionary<string, bool>();

        foreach (var modName in modNames)
        {
            // For now, assume all mods are compatible
            // In a real implementation, this would check mod metadata
            results[modName] = true;
        }

        return results;
    }

    private async Task<ModInjectionResult> InjectLovelyDumpAsync(ModInjectionConfig config)
    {
        var sourceDumpPath = Path.Combine(config.SourceModsPath, "lovely", "dump");
        var targetDumpPath = Path.Combine(config.TargetModsPath, "lovely", "dump");

        if (!Directory.Exists(sourceDumpPath))
        {
            return new ModInjectionResult
            {
                Success = false,
                Errors = new[] { "Lovely dump directory not found" }
            };
        }

        var (filesCopied, bytesCopied) = await CopyDirectoryRecursiveAsync(sourceDumpPath, targetDumpPath);

        return new ModInjectionResult
        {
            Success = true,
            FilesCopied = filesCopied,
            BytesCopied = bytesCopied
        };
    }

    private async Task<ModInjectionResult> InjectSMODSAsync(ModInjectionConfig config)
    {
        var sourceSmodsPath = Path.Combine(config.SourceModsPath, "smods");
        var targetSmodsPath = Path.Combine(config.TargetModsPath, "smods");

        if (!Directory.Exists(sourceSmodsPath))
        {
            return new ModInjectionResult
            {
                Success = false,
                Errors = new[] { "SMODS directory not found" }
            };
        }

        var (filesCopied, bytesCopied) = await CopyDirectoryRecursiveAsync(sourceSmodsPath, targetSmodsPath);

        return new ModInjectionResult
        {
            Success = true,
            FilesCopied = filesCopied,
            BytesCopied = bytesCopied
        };
    }

    private async Task<ModInjectionResult> InjectLibrariesAsync(ModInjectionConfig config)
    {
        var sourceLibsPath = Path.Combine(config.SourceModsPath, "smods", "libs");
        var totalFiles = 0;
        var totalBytes = 0L;
        var errors = new List<string>();

        // Copy nativefs.lua
        var nativefsSource = Path.Combine(sourceLibsPath, "nativefs", "nativefs.lua");
        var nativefsTarget = Path.Combine(config.TargetModsPath, "nativefs.lua");

        if (File.Exists(nativefsSource))
        {
            await CopyFileAsync(nativefsSource, nativefsTarget);
            var fileInfo = new FileInfo(nativefsTarget);
            totalFiles++;
            totalBytes += fileInfo.Length;
        }
        else
        {
            errors.Add("nativefs.lua not found");
        }

        // Copy json.lua
        var jsonSource = Path.Combine(sourceLibsPath, "json", "json.lua");
        var jsonTarget = Path.Combine(config.TargetModsPath, "json.lua");

        if (File.Exists(jsonSource))
        {
            await CopyFileAsync(jsonSource, jsonTarget);
            var fileInfo = new FileInfo(jsonTarget);
            totalFiles++;
            totalBytes += fileInfo.Length;
        }
        else
        {
            errors.Add("json.lua not found");
        }

        return new ModInjectionResult
        {
            Success = !errors.Any(),
            Errors = errors,
            FilesCopied = totalFiles,
            BytesCopied = totalBytes
        };
    }

    private async Task<ModInjectionResult> CreateLovelyConfigAsync(ModInjectionConfig config)
    {
        var lovelyConfig = new
        {
            repo = "https://github.com/ethangreen-dev/lovely-injector",
            version = "0.7.1",
            mod_dir = "/data/data/com.unofficial.balatro/files/save/game/Mods"
        };

        var configPath = Path.Combine(config.TargetModsPath, "lovely.lua");
        var configContent = $"return {System.Text.Json.JsonSerializer.Serialize(lovelyConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}";

        try
        {
            await File.WriteAllTextAsync(configPath, configContent);
            return new ModInjectionResult { Success = true };
        }
        catch (Exception ex)
        {
            return new ModInjectionResult
            {
                Success = false,
                Errors = new[] { $"Failed to create lovely config: {ex.Message}" }
            };
        }
    }

    private async Task<ModInjectionResult> InjectIndividualModsAsync(ModInjectionConfig config)
    {
        var availableMods = await GetAvailableModsAsync();
        var modsToInject = availableMods.Where(mod => !config.ExcludedMods.Contains(mod));
        var injectedComponents = new List<string>();
        var totalFiles = 0;
        var totalBytes = 0L;

        foreach (var modName in modsToInject)
        {
            var sourceModPath = Path.Combine(config.SourceModsPath, modName);
            var targetModPath = Path.Combine(config.TargetModsPath, modName);

            try
            {
                var (filesCopied, bytesCopied) = await CopyDirectoryRecursiveAsync(sourceModPath, targetModPath);
                injectedComponents.Add(modName);
                totalFiles += filesCopied;
                totalBytes += bytesCopied;
            }
            catch (Exception ex)
            {
                // Log error but continue with other mods
                Console.WriteLine($"Warning: Failed to inject mod {modName}: {ex.Message}");
            }
        }

        return new ModInjectionResult
        {
            Success = true,
            InjectedComponents = injectedComponents,
            FilesCopied = totalFiles,
            BytesCopied = totalBytes
        };
    }

    private async Task<(int filesCopied, long bytesCopied)> CopyDirectoryRecursiveAsync(string sourceDir, string targetDir)
    {
        var filesCopied = 0;
        var bytesCopied = 0L;

        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var targetFile = Path.Combine(targetDir, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            await CopyFileAsync(file, targetFile);

            var fileInfo = new FileInfo(targetFile);
            filesCopied++;
            bytesCopied += fileInfo.Length;
        }

        return (filesCopied, bytesCopied);
    }

    private async Task CopyFileAsync(string source, string target)
    {
        using var sourceStream = File.OpenRead(source);
        using var targetStream = File.Create(target);
        await sourceStream.CopyToAsync(targetStream);
    }
}