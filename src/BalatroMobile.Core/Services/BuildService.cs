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
    private readonly string? _love2dApkPath;
    private readonly string? _balatroApkPatchPath;
    private readonly GameExtractor _gameExtractor;

    public BuildService(
        IGameDetector gameDetector,
        IPatchService patchService,
        IModInjectionService modInjectionService,
        IApkTool apkTool,
        IJavaTool javaTool,
        string? love2dApkPath = null,
        string? balatroApkPatchPath = null)
    {
        _gameDetector = gameDetector;
        _patchService = patchService;
        _modInjectionService = modInjectionService;
        _apkTool = apkTool;
        _javaTool = javaTool;
        _love2dApkPath = love2dApkPath;
        _balatroApkPatchPath = balatroApkPatchPath;
        _gameExtractor = new GameExtractor(msg => Console.WriteLine($"  [Extract] {msg}"));
    }

    public async Task<BuildResult> BuildAsync(BuildConfig config, IProgress<string>? progress = null)
    {
        var startTime = DateTime.Now;
        var messages = new List<string>();
        var errors = new List<string>();
        
        // Use a persistent temp directory for debugging
        var tempDir = Path.Combine(Path.GetTempPath(), "BalatroMobile", $"build_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(tempDir);

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

            // Step 2: Locate Balatro installation
            progress?.Report("Locating Balatro...");
            var balatroPath = await _gameDetector.GetGameInstallPathAsync();
            if (string.IsNullOrEmpty(balatroPath))
            {
                errors.Add("Balatro installation not found");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            messages.Add($"Found Balatro at: {balatroPath}");

            // Step 3: Extract game content from Balatro.exe
            progress?.Report("Extracting game content from Balatro.exe...");
            var extractPath = Path.Combine(tempDir, "extracted");
            var balatroExePath = Path.Combine(balatroPath, "Balatro.exe");
            
            if (!await _gameExtractor.ExtractGameAsync(balatroExePath, extractPath))
            {
                errors.Add("Failed to extract game content from Balatro.exe");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            
            // Verify extraction
            var extractedLuaFiles = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories);
            messages.Add($"Extracted {extractedLuaFiles.Length} Lua files");
            
            if (extractedLuaFiles.Length == 0)
            {
                errors.Add("No Lua files found after extraction - extraction may have failed");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }

            // Step 4: Apply patches to the extracted game files
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
                    // Log warning but continue - some patches might not apply to all versions
                    messages.Add($"Note: Patch '{patchResult.Description}' skipped: {patchResult.Error}");
                }
            }

            // Step 5: Create game.love from patched content
            progress?.Report("Creating game.love package...");
            var gameLovePath = Path.Combine(tempDir, "game.love");
            
            if (!await _gameExtractor.CreateGameLoveAsync(extractPath, gameLovePath))
            {
                errors.Add("Failed to create game.love package");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            
            var gameLoveInfo = new FileInfo(gameLovePath);
            messages.Add($"Created game.love: {gameLoveInfo.Length / 1024.0 / 1024.0:F2} MB");

            // Step 6: Build APK
            progress?.Report("Building Android APK...");
            var outputPath = await BuildAndroidApkAsync(config, gameLovePath, tempDir);
            if (string.IsNullOrEmpty(outputPath))
            {
                errors.Add("Failed to build Android APK");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            
            var apkInfo = new FileInfo(outputPath);
            messages.Add($"Built APK: {apkInfo.Length / 1024.0 / 1024.0:F2} MB");

            progress?.Report("Build completed successfully!");
            return CreateResult(true, outputPath, messages, errors, DateTime.Now - startTime);
        }
        catch (Exception ex)
        {
            errors.Add($"Build failed with exception: {ex.Message}");
            errors.Add($"Stack trace: {ex.StackTrace}");
            return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
        }
        finally
        {
            // Optionally cleanup temp directory
            // For now, keep it for debugging
            // try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    public async Task<bool> ValidateBuildEnvironmentAsync()
    {
        // Check if all required tools are available
        var apkToolAvailable = await _apkTool.IsAvailableAsync();
        var javaAvailable = await _javaTool.IsAvailableAsync();
        var balatroPath = await _gameDetector.GetGameInstallPathAsync();
        var balatroAvailable = balatroPath != null;

        return apkToolAvailable && javaAvailable && balatroAvailable;
    }

    public IEnumerable<string> GetSupportedPlatforms()
    {
        return new[] { "Android" };
    }

    private async Task<string?> BuildAndroidApkAsync(BuildConfig config, string gameLovePath, string tempDir)
    {
        try
        {
            var outputPath = config.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = "balatro.apk";
            }

            // Make output path absolute if relative
            if (!Path.IsPathRooted(outputPath))
            {
                outputPath = Path.Combine(Environment.CurrentDirectory, outputPath);
            }

            // Step 1: Get base APK path
            var baseApkPath = _love2dApkPath;
            if (string.IsNullOrEmpty(baseApkPath) || !File.Exists(baseApkPath))
            {
                Console.WriteLine($"ERROR: Love2D APK not found at: {baseApkPath}");
                return null;
            }
            Console.WriteLine($"Using base APK: {baseApkPath} ({new FileInfo(baseApkPath).Length / 1024.0 / 1024.0:F2} MB)");

            // Step 2: Decompile APK
            Console.WriteLine("Decompiling APK with APKTool...");
            var decompiledPath = Path.Combine(tempDir, "decompiled");
            var decompileSuccess = await _apkTool.DecompileAsync(baseApkPath, decompiledPath);
            if (!decompileSuccess)
            {
                Console.WriteLine("ERROR: Failed to decompile APK");
                return null;
            }
            Console.WriteLine($"Decompiled to: {decompiledPath}");

            // Step 3: Copy game.love to assets
            Console.WriteLine("Adding game.love to APK assets...");
            var assetsPath = Path.Combine(decompiledPath, "assets");
            Directory.CreateDirectory(assetsPath);
            var targetGameLovePath = Path.Combine(assetsPath, "game.love");
            
            if (File.Exists(gameLovePath))
            {
                File.Copy(gameLovePath, targetGameLovePath, true);
                var loveSizeAfterCopy = new FileInfo(targetGameLovePath).Length;
                Console.WriteLine($"Copied game.love: {loveSizeAfterCopy / 1024.0 / 1024.0:F2} MB");
            }
            else
            {
                Console.WriteLine($"ERROR: game.love not found at: {gameLovePath}");
                return null;
            }

            // Step 4: Apply Balatro APK patches if available
            if (!string.IsNullOrEmpty(_balatroApkPatchPath) && Directory.Exists(_balatroApkPatchPath))
            {
                Console.WriteLine("Applying Balatro APK patches...");
                await ApplyBalatroApkPatchesAsync(decompiledPath);
            }

            // Step 5: Inject mods if requested
            if (config.InjectMods)
            {
                Console.WriteLine("Injecting mods...");
                await InjectModsIntoApkAsync(config, decompiledPath);
            }

            // Step 6: Apply AndroidManifest patches
            Console.WriteLine("Patching AndroidManifest...");
            await ApplyAndroidManifestPatchesAsync(decompiledPath);

            // Step 7: Recompile APK
            Console.WriteLine("Recompiling APK...");
            var unsignedApkPath = Path.Combine(tempDir, "unsigned.apk");
            var recompileSuccess = await _apkTool.CompileAsync(decompiledPath, unsignedApkPath);
            if (!recompileSuccess)
            {
                Console.WriteLine("ERROR: Failed to recompile APK");
                return null;
            }
            Console.WriteLine($"Recompiled APK: {new FileInfo(unsignedApkPath).Length / 1024.0 / 1024.0:F2} MB");

            // Step 8: Sign APK
            Console.WriteLine("Signing APK...");
            var signSuccess = await _apkTool.SignAsync(unsignedApkPath);
            
            // Look for signed APK (uber-apk-signer creates a new file)
            var signedApkPath = Path.Combine(tempDir, "unsigned-aligned-signed.apk");
            if (!File.Exists(signedApkPath))
            {
                signedApkPath = Path.Combine(tempDir, "unsigned-signed.apk");
            }
            if (!File.Exists(signedApkPath))
            {
                // Use unsigned APK if signing failed
                signedApkPath = unsignedApkPath;
                Console.WriteLine("Warning: Using unsigned APK (signing may have failed)");
            }

            // Copy to final output location
            File.Copy(signedApkPath, outputPath, true);
            Console.WriteLine($"Final APK: {outputPath} ({new FileInfo(outputPath).Length / 1024.0 / 1024.0:F2} MB)");

            return File.Exists(outputPath) ? outputPath : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR building APK: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return null;
        }
    }

    private async Task ApplyBalatroApkPatchesAsync(string decompiledApkPath)
    {
        if (string.IsNullOrEmpty(_balatroApkPatchPath) || !Directory.Exists(_balatroApkPatchPath))
        {
            return;
        }

        try
        {
            // Copy all patch files to the decompiled APK
            foreach (var file in Directory.GetFiles(_balatroApkPatchPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(_balatroApkPatchPath, file);
                var targetPath = Path.Combine(decompiledApkPath, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath, true);
            }
            Console.WriteLine("Applied Balatro APK patches");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to apply Balatro APK patches: {ex.Message}");
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
        var result = await _modInjectionService.InjectModsAsync(modConfig);
        
        if (result.Success)
        {
            Console.WriteLine($"Injected {result.InjectedComponents.Count()} mod components ({result.BytesCopied / 1024.0 / 1024.0:F2} MB)");
        }
        else
        {
            Console.WriteLine($"Warning: Mod injection had errors: {string.Join(", ", result.Errors)}");
        }
    }

    private async Task ApplyAndroidManifestPatchesAsync(string decompiledApkPath)
    {
        var manifestPath = Path.Combine(decompiledApkPath, "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine("Warning: AndroidManifest.xml not found");
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(manifestPath);
            
            // Change package name to avoid conflicts with official LÖVE app
            content = content.Replace("org.love2d.android", "com.unofficial.balatro");
            
            // Change app name
            content = content.Replace("android:label=\"LÖVE for Android\"", "android:label=\"Balatro\"");
            
            await File.WriteAllTextAsync(manifestPath, content);
            Console.WriteLine("Patched AndroidManifest.xml");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to patch AndroidManifest: {ex.Message}");
        }
    }

    private IEnumerable<PatchConfig> GetPatchesForConfig(BuildConfig config)
    {
        var patches = new List<PatchConfig>();

        // Mobile compatibility patches - these are applied to the Lua source code
        patches.Add(new PatchConfig
        {
            FilePath = "globals.lua",
            SearchPattern = "self:set_language",
            Replacement = @"-- Mobile compatibility patch
    if love.system.getOS() == 'Android' or love.system.getOS() == 'iOS' then
        self.F_SAVE_TIMER = 5
        self.F_DISCORD = true
        self.F_NO_ACHIEVEMENTS = true
        self.F_CRASH_REPORTS = false
        self.F_SOUND_THREAD = true
        self.F_VIDEO_SETTINGS = false
        self.F_ENGLISH_ONLY = false
        self.F_QUIT_BUTTON = false
    end
    self:set_language",
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

        // High DPI patch
        if (config.EnableHighDpi)
        {
            patches.Add(new PatchConfig
            {
                FilePath = "conf.lua",
                SearchPattern = "t.window.usedpiscale",
                Replacement = "    t.window.usedpiscale = false",
                Description = "High DPI configuration"
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
                SearchPattern = "t.externalstorage",
                Replacement = "    t.externalstorage = true",
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
