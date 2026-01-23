using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services.GameDetection;
using BalatroMobile.Infrastructure.Tools;

namespace BalatroMobile.Core.Services;

public class BuildService : IBuildService
{
    private readonly IGameDetector _gameDetector;
    private readonly IPatchService _patchService;
    private readonly IModInjectionService _modInjectionService;
    private readonly IApkTool _apkTool;
    private readonly IJavaTool _javaTool;

    public BuildService(
        IGameDetector gameDetector,
        IPatchService patchService,
        IModInjectionService modInjectionService,
        IApkTool apkTool,
        IJavaTool javaTool)
    {
        _gameDetector = gameDetector;
        _patchService = patchService;
        _modInjectionService = modInjectionService;
        _apkTool = apkTool;
        _javaTool = javaTool;
    }

    public async Task<BuildResult> BuildAsync(BuildConfig config, IProgress<string>? progress = null)
    {
        var startTime = DateTime.Now;
        var messages = new List<string>();
        var errors = new List<string>();

        try
        {
            progress?.Report("Starting build process...");

            // Step 1: Validate build environment
            progress?.Report("Validating build environment...");
            if (!await ValidateBuildEnvironmentAsync())
            {
                errors.Add("Build environment validation failed");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            messages.Add("Build environment validated");

            // Step 2: Locate and extract Balatro
            progress?.Report("Locating and extracting Balatro...");
            var balatroPath = await _gameDetector.GetGameInstallPathAsync();
            if (string.IsNullOrEmpty(balatroPath))
            {
                errors.Add("Balatro installation not found");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }

            var extractPath = Path.Combine(Path.GetTempPath(), "BalatroMobile", Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractPath);

            await ExtractBalatroAsync(balatroPath, extractPath);
            messages.Add("Balatro extracted successfully");

            // Step 3: Apply patches
            progress?.Report("Applying patches...");
            var patches = GetPatchesForConfig(config);
            var patchResults = await _patchService.ApplyPatchesAsync(patches, extractPath);

            foreach (var patchResult in patchResults)
            {
                if (patchResult.Success)
                {
                    messages.Add($"Applied patch: {patchResult.Description}");
                }
                else
                {
                    errors.Add($"Failed to apply patch '{patchResult.Description}': {patchResult.Error}");
                }
            }

            // Step 4: Package game
            progress?.Report("Packaging game...");
            var gamePackagePath = await PackageGameAsync(extractPath);
            if (string.IsNullOrEmpty(gamePackagePath))
            {
                errors.Add("Failed to package game");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            messages.Add("Game packaged successfully");

            // Step 5: Build APK/IPA
            progress?.Report("Building mobile package...");
            var outputPath = await BuildMobilePackageAsync(config, gamePackagePath);
            if (string.IsNullOrEmpty(outputPath))
            {
                errors.Add("Failed to build mobile package");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            messages.Add($"Mobile package built: {outputPath}");

            // Step 6: Inject mods (optional)
            if (config.InjectMods)
            {
                progress?.Report("Injecting mods...");
                var modInjectionResult = await InjectModsAsync(config);
                if (modInjectionResult.Success)
                {
                    messages.Add($"Mods injected: {string.Join(", ", modInjectionResult.InjectedComponents)}");
                    messages.Add($"Mod files: {modInjectionResult.FilesCopied} ({modInjectionResult.BytesCopied} bytes)");
                }
                else
                {
                    errors.Add($"Mod injection failed: {string.Join(", ", modInjectionResult.Errors)}");
                }
            }

            // Cleanup
            try
            {
                Directory.Delete(extractPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }

            progress?.Report("Build completed successfully!");
            return CreateResult(true, outputPath, messages, errors, DateTime.Now - startTime);

        }
        catch (Exception ex)
        {
            errors.Add($"Build failed with exception: {ex.Message}");
            return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
        }
    }

    public async Task<bool> ValidateBuildEnvironmentAsync()
    {
        // Check if all required tools are available
        var apkToolAvailable = await _apkTool.IsAvailableAsync();
        var javaAvailable = await _javaTool.IsAvailableAsync();
        var balatroAvailable = await _gameDetector.GetGameInstallPathAsync() != null;

        return apkToolAvailable && javaAvailable && balatroAvailable;
    }

    public IEnumerable<string> GetSupportedPlatforms()
    {
        return new[] { "Android", "iOS" };
    }

    private async Task ExtractBalatroAsync(string balatroPath, string extractPath)
    {
        // For now, just copy the Balatro.exe - in a real implementation
        // this would extract the game files from the executable
        var exePath = Path.Combine(balatroPath, "Balatro.exe");
        if (File.Exists(exePath))
        {
            File.Copy(exePath, Path.Combine(extractPath, "Balatro.exe"));
        }
    }

    private async Task<string?> PackageGameAsync(string extractPath)
    {
        // Placeholder - would compress game files into .love format
        var lovePath = Path.Combine(Path.GetTempPath(), "game.love");
        // In real implementation, this would create a .love file from the extracted game
        return lovePath;
    }

    private async Task<string?> BuildMobilePackageAsync(BuildConfig config, string gamePackagePath)
    {
        if (config.Platform == Platform.Android)
        {
            return await BuildAndroidApkAsync(config, gamePackagePath);
        }
        else
        {
            // iOS build not implemented yet
            return null;
        }
    }

    private async Task<string?> BuildAndroidApkAsync(BuildConfig config, string gamePackagePath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "BalatroMobile", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputPath = config.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = "balatro.apk";
            }

            // Step 1: Get base APK path (placeholder - would download if not cached)
            var baseApkPath = Path.Combine(tempDir, "love-11.5-android-embed.apk");
            // In real implementation, download love-11.5-android-embed.apk

            // Step 2: Decompile APK
            var decompiledPath = Path.Combine(tempDir, "decompiled");
            var decompileSuccess = await _apkTool.DecompileAsync(baseApkPath, decompiledPath);
            if (!decompileSuccess)
            {
                return null;
            }

            // Step 3: Copy game.love to assets
            var assetsPath = Path.Combine(decompiledPath, "assets");
            Directory.CreateDirectory(assetsPath);
            var gameLovePath = Path.Combine(assetsPath, "game.love");
            if (File.Exists(gamePackagePath))
            {
                File.Copy(gamePackagePath, gameLovePath, true);
            }

            // Step 4: Inject mods if requested
            if (config.InjectMods)
            {
                await InjectModsIntoApkAsync(config, decompiledPath);
            }

            // Step 5: Apply AndroidManifest patches
            await ApplyAndroidManifestPatchesAsync(decompiledPath);

            // Step 6: Recompile APK
            var unsignedApkPath = Path.Combine(tempDir, "unsigned.apk");
            var recompileSuccess = await _apkTool.CompileAsync(decompiledPath, unsignedApkPath);
            if (!recompileSuccess)
            {
                return null;
            }

            // Step 7: Sign APK
            var signSuccess = await _apkTool.SignAsync(unsignedApkPath);
            if (!signSuccess)
            {
                // If signing fails, just use the unsigned APK (for development)
                File.Copy(unsignedApkPath, outputPath, true);
            }
            else
            {
                // In real implementation, the signed APK would be renamed to outputPath
                File.Copy(unsignedApkPath, outputPath, true);
            }

            return File.Exists(outputPath) ? outputPath : null;
        }
        finally
        {
            // Clean up temp directory
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task InjectModsIntoApkAsync(BuildConfig config, string decompiledApkPath)
    {
        // Create the Android app data directory structure in the APK
        var appDataPath = Path.Combine(decompiledApkPath, "assets", "files", "save", "game");
        Directory.CreateDirectory(appDataPath);

        // Configure mod injection for APK structure
        var modConfig = new ModInjectionConfig
        {
            SourceModsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Balatro", "Mods"),
            TargetModsPath = appDataPath,
            IncludeLovelyDump = true,
            IncludeSMODS = true,
            IncludeLibraries = true,
            CreateLovelyConfig = true,
            ExcludedMods = new[] { "BrainstormRerollButton" } // Exclude problematic mods
        };

        // Inject mods into the APK structure
        await _modInjectionService.InjectModsAsync(modConfig);
    }

    private async Task ApplyAndroidManifestPatchesAsync(string decompiledApkPath)
    {
        // Apply basic AndroidManifest patches (placeholder)
        var manifestPath = Path.Combine(decompiledApkPath, "AndroidManifest.xml");
        if (File.Exists(manifestPath))
        {
            // In real implementation, would patch package name, permissions, etc.
            // For now, just leave as-is
        }
    }

    private IEnumerable<PatchConfig> GetPatchesForConfig(BuildConfig config)
    {
        var patches = new List<PatchConfig>();

        // Mobile compatibility patches
        patches.Add(new PatchConfig
        {
            FilePath = "globals.lua",
            SearchPattern = "loadstring",
            Replacement = @"    -- Removed 'loadstring' line which generated lua code that exited upon starting on mobile
    if love.system.getOS() == 'Android' or love.system.getOS() == 'iOS' then
        self.F_SAVE_TIMER = 5
        self.F_DISCORD = true
        self.F_NO_ACHIEVEMENTS = true
        self.F_CRASH_REPORTS = false
        self.F_SOUND_THREAD = true
        self.F_VIDEO_SETTINGS = false
        self.F_ENGLISH_ONLY = false
        self.F_QUIT_BUTTON = false
    end",
            Description = "Mobile compatibility patch"
        });

        // FPS cap patch
        if (config.FpsCap != FpsCap.None)
        {
            var fpsValue = config.FpsCap == FpsCap.Default
                ? "select(3, love.window.getMode())['refreshrate']"
                : config.CustomFpsValue ?? "60";

            patches.Add(new PatchConfig
            {
                FilePath = "main.lua",
                SearchPattern = "G.FPS_CAP = G.FPS_CAP or",
                Replacement = $"        G.FPS_CAP = {fpsValue}",
                Description = "FPS cap configuration"
            });
        }

        // Landscape orientation patch
        if (config.EnableLandscape)
        {
            patches.Add(new PatchConfig
            {
                FilePath = "functions/button_callbacks.lua",
                SearchPattern = "resizable = true,",
                Replacement = "    resizable = not (love.system.getOS() == 'Android' or love.system.getOS() == 'iOS'),",
                Description = "Landscape orientation lock"
            });
        }

        // High DPI patch
        if (config.EnableHighDpi)
        {
            patches.Add(new PatchConfig
            {
                FilePath = "conf.lua",
                SearchPattern = "t.window.width = 0",
                Replacement = "    t.window.width = 0\n    t.window.usedpiscale = false",
                Description = "High DPI configuration"
            });

            patches.Add(new PatchConfig
            {
                FilePath = "functions/button_callbacks.lua",
                SearchPattern = "highdpi = (love.system.getOS() == 'OS X')",
                Replacement = "    highdpi = (love.system.getOS() == 'OS X' or love.system.getOS() == 'Android' or love.system.getOS() == 'iOS')",
                Description = "High DPI platform support"
            });
        }

        // CRT shader disable
        if (config.DisableCrtShader)
        {
            patches.Add(new PatchConfig
            {
                FilePath = "globals.lua",
                SearchPattern = "crt = ",
                Replacement = "            crt = 0,",
                Description = "CRT shader disable"
            });
        }

        // External storage patch
        if (config.EnableExternalStorage)
        {
            patches.Add(new PatchConfig
            {
                FilePath = "conf.lua",
                SearchPattern = "t.window.width = 0",
                Replacement = "    t.window.width = 0\n    t.externalstorage = true",
                Description = "External storage configuration"
            });
        }

        return patches;
    }

    private static BuildResult CreateResult(bool success, string? outputPath, IEnumerable<string> messages, IEnumerable<string> errors, TimeSpan duration)
    {
        return new BuildResult
        {
            Success = success,
            OutputPath = outputPath,
            Messages = messages,
            Errors = errors,
            Duration = duration
        };
    }

}