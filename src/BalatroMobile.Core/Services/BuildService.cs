using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services.GameDetection;
using BalatroMobile.Infrastructure.Tools;

namespace BalatroMobile.Core.Services;

public class BuildService : IBuildService
{
    // #region agent log
    private static readonly string _debugLogPath = Path.Combine(Environment.CurrentDirectory, "debug.log");
    private static void DebugLog(string hypothesisId, string message, object? data = null)
    {
        try
        {
            var entry = System.Text.Json.JsonSerializer.Serialize(new { hypothesisId, location = "BuildService.cs", message, data, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sessionId = "debug-session" });
            File.AppendAllText(_debugLogPath, entry + "\n");
        }
        catch { }
    }
    // #endregion
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
        
        // Temp directory for build process (in system temp, auto-cleaned)
        var tempDir = Path.Combine(Path.GetTempPath(), "BalatroMobile", $"build_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(tempDir);

        try
        {
            progress?.Report("Starting build process...");
            
            // Show where output will go
            var outputPath = config.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = "balatro.apk";
            }
            if (!Path.IsPathRooted(outputPath))
            {
                outputPath = Path.Combine(Environment.CurrentDirectory, outputPath);
            }
            Console.WriteLine();
            Console.WriteLine("=== BUILD PATHS ===");
            Console.WriteLine($"  Output APK will be: {outputPath}");
            Console.WriteLine($"  Temp work folder:   {tempDir}");
            Console.WriteLine();

            // Step 1: Validate build environment
            progress?.Report("Validating build environment...");
            if (!await ValidateBuildEnvironmentAsync())
            {
                errors.Add("Build environment validation failed");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            messages.Add("Build environment validated");

            // Step 2: Locate Balatro.exe or Game.love in current directory
            progress?.Report("Locating game files...");
            
            // Check for game files in current directory
            var currentDir = Environment.CurrentDirectory;
            var balatroExePath = Path.Combine(currentDir, "Balatro.exe");
            var inputLovePath = Path.Combine(currentDir, "Game.love");
            
            string gameFilePath;
            if (File.Exists(balatroExePath))
            {
                gameFilePath = balatroExePath;
            }
            else if (File.Exists(inputLovePath))
            {
                gameFilePath = inputLovePath;
            }
            else
            {
                errors.Add("Balatro.exe or Game.love not found in current directory");
                errors.Add($"Please copy Balatro.exe to: {currentDir}");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            
            var gameFileInfo = new FileInfo(gameFilePath);
            Console.WriteLine($"  Source file:        {gameFilePath}");
            Console.WriteLine($"  File size:          {gameFileInfo.Length / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine();
            messages.Add($"Using game file: {Path.GetFileName(gameFilePath)} ({gameFileInfo.Length / 1024.0 / 1024.0:F2} MB)");

            // Step 3: Extract game content
            progress?.Report($"Extracting game content from {Path.GetFileName(gameFilePath)}...");
            var extractPath = Path.Combine(tempDir, "extracted");
            
            if (!await _gameExtractor.ExtractGameAsync(gameFilePath, extractPath))
            {
                errors.Add("Failed to extract game content from Balatro.exe");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            
            // Verify extraction
            var extractedLuaFiles = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories);
            Console.WriteLine($"  Extracted to temp:  {extractPath}");
            Console.WriteLine($"  Files extracted:    {extractedLuaFiles.Length} Lua files");
            messages.Add($"Extracted {extractedLuaFiles.Length} Lua files");
            
            // #region agent log
            // Log critical file existence and content check
            var globalsPath = Path.Combine(extractPath, "globals.lua");
            var mainPath = Path.Combine(extractPath, "main.lua");
            var confPath = Path.Combine(extractPath, "conf.lua");
            var buttonCallbacksPath = Path.Combine(extractPath, "functions", "button_callbacks.lua");
            var flameShaderPath = Path.Combine(extractPath, "resources", "shaders", "flame.fs");
            
            DebugLog("D", "BuildService extraction verification", new { 
                luaFilesCount = extractedLuaFiles.Length,
                globalsExists = File.Exists(globalsPath),
                mainExists = File.Exists(mainPath),
                confExists = File.Exists(confPath),
                buttonCallbacksExists = File.Exists(buttonCallbacksPath),
                flameShaderExists = File.Exists(flameShaderPath)
            });
            
            // Check if globals.lua contains 'loadstring' (our critical patch target)
            if (File.Exists(globalsPath))
            {
                var globalsContent = File.ReadAllText(globalsPath);
                var containsLoadstring = globalsContent.Contains("loadstring");
                var loadstringLineNum = -1;
                var lines = globalsContent.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("loadstring"))
                    {
                        loadstringLineNum = i + 1;
                        break;
                    }
                }
                DebugLog("E", "globals.lua loadstring check BEFORE patching", new { containsLoadstring, loadstringLineNum, globalsLength = globalsContent.Length });
            }
            // #endregion
            
            if (extractedLuaFiles.Length == 0)
            {
                errors.Add("No Lua files found after extraction - extraction may have failed");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }

            // Step 4: Apply patches to the extracted game files (NOT the original)
            progress?.Report("Applying patches to extracted files...");
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
            
            // #region agent log
            // Verify critical patch was applied - globals.lua should NOT have loadstring anymore
            var globalsPathPost = Path.Combine(extractPath, "globals.lua");
            if (File.Exists(globalsPathPost))
            {
                var globalsContentPost = File.ReadAllText(globalsPathPost);
                var stillContainsLoadstring = globalsContentPost.Contains("loadstring");
                var containsMobileBlock = globalsContentPost.Contains("love.system.getOS() == 'Android'");
                DebugLog("E", "globals.lua AFTER patching", new { stillContainsLoadstring, containsMobileBlock, globalsLengthPost = globalsContentPost.Length });
                
                // Also log first 500 chars of file to see structure
                DebugLog("E", "globals.lua content sample", new { first500 = globalsContentPost.Substring(0, Math.Min(500, globalsContentPost.Length)) });
            }
            // #endregion

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
            var finalApkPath = await BuildAndroidApkAsync(config, gameLovePath, tempDir);
            if (string.IsNullOrEmpty(finalApkPath))
            {
                errors.Add("Failed to build Android APK");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            
            var apkInfo = new FileInfo(finalApkPath);
            messages.Add($"Built APK: {apkInfo.Length / 1024.0 / 1024.0:F2} MB");

            progress?.Report("Build completed successfully!");
            return CreateResult(true, finalApkPath, messages, errors, DateTime.Now - startTime);
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
            // uber-apk-signer naming: {input}-aligned-debugSigned.apk
            // Input: unsigned.apk -> Output: unsigned-aligned-debugSigned.apk
            var signedApkPath = Path.Combine(tempDir, "unsigned-aligned-debugSigned.apk");
            
            // #region agent log
            // Log what files exist in temp dir after signing
            var tempFiles = Directory.GetFiles(tempDir, "*.apk");
            DebugLog("F", "After signing - APK files in temp dir", new { tempDir, apkFiles = tempFiles.Select(Path.GetFileName).ToArray() });
            DebugLog("F", "Looking for signed APK", new { expectedPath = signedApkPath, exists = File.Exists(signedApkPath) });
            // #endregion
            
            if (!File.Exists(signedApkPath))
            {
                // Try alternative naming patterns
                signedApkPath = Path.Combine(tempDir, "unsigned-debugSigned.apk");
                // #region agent log
                DebugLog("F", "Trying alternative 1", new { path = signedApkPath, exists = File.Exists(signedApkPath) });
                // #endregion
            }
            if (!File.Exists(signedApkPath))
            {
                signedApkPath = Path.Combine(tempDir, "unsigned-aligned-signed.apk");
                // #region agent log
                DebugLog("F", "Trying alternative 2", new { path = signedApkPath, exists = File.Exists(signedApkPath) });
                // #endregion
            }
            if (!File.Exists(signedApkPath))
            {
                // Use unsigned APK if signing failed - THIS WILL NOT INSTALL ON ANDROID!
                signedApkPath = unsignedApkPath;
                Console.WriteLine("ERROR: Signed APK not found! Using unsigned APK (will NOT install on Android)");
                // #region agent log
                DebugLog("F", "SIGNING FAILED - using unsigned APK", new { unsignedApkPath });
                // #endregion
            }
            else
            {
                Console.WriteLine($"Using signed APK: {Path.GetFileName(signedApkPath)}");
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
        // #region agent log
        DebugLog("C", "ApplyBalatroApkPatchesAsync START", new { _balatroApkPatchPath, decompiledApkPath, patchExists = Directory.Exists(_balatroApkPatchPath ?? "") });
        // #endregion

        if (string.IsNullOrEmpty(_balatroApkPatchPath) || !Directory.Exists(_balatroApkPatchPath))
        {
            // #region agent log
            DebugLog("C", "ApplyBalatroApkPatchesAsync SKIPPED - no patch path");
            // #endregion
            return;
        }

        try
        {
            // Copy ONLY the specific files the original tool copies
            // Original tool: Patching.cs lines 170-175
            var filesToCopy = new[]
            {
                "AndroidManifest.xml",
                "res/drawable-hdpi/love.png",
                "res/drawable-mdpi/love.png",
                "res/drawable-xhdpi/love.png",
                "res/drawable-xxhdpi/love.png",
                "res/drawable-xxxhdpi/love.png"
            };

            var filesCopied = new List<string>();
            foreach (var relPath in filesToCopy)
            {
                var sourcePath = Path.Combine(_balatroApkPatchPath, relPath);
                var targetPath = Path.Combine(decompiledApkPath, relPath);
                
                if (File.Exists(sourcePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    File.Copy(sourcePath, targetPath, true);
                    filesCopied.Add(relPath);
                }
            }
            
            // #region agent log
            DebugLog("C", "ApplyBalatroApkPatchesAsync DONE", new { filesCopied });
            // #endregion
            
            Console.WriteLine($"Applied Balatro APK patches: {string.Join(", ", filesCopied)}");
        }
        catch (Exception ex)
        {
            // #region agent log
            DebugLog("C", "ApplyBalatroApkPatchesAsync ERROR", new { error = ex.Message });
            // #endregion
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

        // =====================================================
        // MANDATORY PATCHES - Always applied for mobile to work
        // These match EXACTLY what blake502/balatro-mobile-maker does
        // The PatchService replaces the ENTIRE LINE containing the search pattern
        // =====================================================

        // 1. CRITICAL: Mobile Exit Fix (Patching.cs line 61-71)
        // The original game has obfuscated code that exits on mobile platforms
        // Search for line containing "loadstring" and replace entire line
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
            Description = "Mobile exit fix (CRITICAL)"
        });

        // 2. On-screen keyboard support (Patching.cs line 73)
        patches.Add(new PatchConfig
        {
            FilePath = "functions/button_callbacks.lua",
            SearchPattern = "G.CONTROLLER.text_input_hook == e and G.CONTROLLER.HID.controller",
            Replacement = "  if G.CONTROLLER.text_input_hook == e and (G.CONTROLLER.HID.controller or G.CONTROLLER.HID.touch) then",
            Description = "On-screen keyboard support"
        });

        // 3. Flame shader GL_ES fix (Patching.cs line 76)
        // Replaces the first line containing "#endif" with the GL_ES block
        patches.Add(new PatchConfig
        {
            FilePath = "resources/shaders/flame.fs",
            SearchPattern = "#endif",
            Replacement = "#endif\n#ifdef GL_ES\n\tprecision MY_HIGHP_OR_MEDIUMP float;\n#endif",
            Description = "Flame shader GL_ES fix"
        });

        // 4. Flame shader function signature fix (Patching.cs line 77)
        patches.Add(new PatchConfig
        {
            FilePath = "resources/shaders/flame.fs",
            SearchPattern = "vec4 effect( vec4 colour, Image texture, vec2 texture_coords, vec2 screen_coords )",
            Replacement = "mediump vec4 effect( mediump vec4 colour, Image texture, mediump vec2 texture_coords, mediump vec2 screen_coords )",
            Description = "Flame shader precision fix"
        });

        // =====================================================
        // OPTIONAL PATCHES - User configurable
        // =====================================================

        // FPS cap patch (Patching.cs line 110/115)
        if (config.FpsCap != FpsCap.None)
        {
            var fpsValue = config.FpsCap == FpsCap.Default
                ? "G.FPS_CAP or select(3, love.window.getMode())['refreshrate']"
                : config.CustomFpsValue ?? "60";

            patches.Add(new PatchConfig
            {
                FilePath = "main.lua",
                SearchPattern = "G.FPS_CAP = G.FPS_CAP or",
                Replacement = $"        G.FPS_CAP = {fpsValue}",
                Description = "FPS cap configuration"
            });
        }

        // Landscape lock (Patching.cs line 123)
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

        // High DPI patches (Patching.cs lines 130-131)
        if (config.EnableHighDpi)
        {
            // Part 1: conf.lua - add usedpiscale after t.window.width = 0
            patches.Add(new PatchConfig
            {
                FilePath = "conf.lua",
                SearchPattern = "t.window.width = 0",
                Replacement = "    t.window.width = 0\n    t.window.usedpiscale = false",
                Description = "High DPI conf.lua patch"
            });

            // Part 2: button_callbacks.lua - enable highdpi for mobile
            patches.Add(new PatchConfig
            {
                FilePath = "functions/button_callbacks.lua",
                SearchPattern = "highdpi = (love.system.getOS() == 'OS X')",
                Replacement = "    highdpi = (love.system.getOS() == 'OS X' or love.system.getOS() == 'Android' or love.system.getOS() == 'iOS')",
                Description = "High DPI button_callbacks patch"
            });
        }

        // CRT shader disable (Patching.cs lines 136-137)
        if (config.DisableCrtShader)
        {
            // First patch: set crt = 0 in globals.lua
            patches.Add(new PatchConfig
            {
                FilePath = "globals.lua",
                SearchPattern = "crt = ",
                Replacement = "            crt = 0,",
                Description = "CRT shader value disable"
            });

            // Second patch: remove setShader call in game.lua
            patches.Add(new PatchConfig
            {
                FilePath = "game.lua",
                SearchPattern = "G.SHADERS['CRT'])",
                Replacement = "",
                Description = "CRT shader call disable"
            });
        }

        // External storage patch (Patching.cs line 142)
        // Note: Original uses "t.window.width = 0" as search pattern
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
