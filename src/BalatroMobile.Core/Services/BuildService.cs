using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services.GameDetection;
using BalatroMobile.Infrastructure.Tools;

namespace BalatroMobile.Core.Services;

public class BuildService : IBuildService
{
    private readonly IGameDetector _gameDetector;
    private readonly IPatchService _patchService;
    private readonly IApkTool _apkTool;
    private readonly IJavaTool _javaTool;
    private readonly string? _love2dApkPath;
    private readonly string? _balatroApkPatchPath;
    private readonly GameExtractor _gameExtractor;

    public BuildService(
        IGameDetector gameDetector,
        IPatchService patchService,
        IApkTool apkTool,
        IJavaTool javaTool,
        string? love2dApkPath = null,
        string? balatroApkPatchPath = null)
    {
        _gameDetector = gameDetector;
        _patchService = patchService;
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

            // Step 4b: Check for BalatroMobileCompat BEFORE bundling (MANDATORY)
            progress?.Report("Checking for BalatroMobileCompat...");
            var balatroAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Balatro"
            );
            var modsPath = Path.Combine(balatroAppData, "Mods");
            var mobileCompatPath = Path.Combine(modsPath, "BalatroMobileCompat");
            
            if (!Directory.Exists(mobileCompatPath))
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  ERROR: BalatroMobileCompat NOT FOUND!                       ║");
                Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║  This mod is REQUIRED for mods to work on mobile.            ║");
                Console.WriteLine("║                                                              ║");
                Console.WriteLine("║  Download from:                                              ║");
                Console.WriteLine("║  https://github.com/eeve-lyn/BalatroMobileCompat             ║");
                Console.WriteLine("║                                                              ║");
                Console.WriteLine("║  Install to your Mods folder:                                ║");
                Console.WriteLine($"║  {modsPath,-60} ║");
                Console.WriteLine("║                                                              ║");
                Console.WriteLine("║  Then run this tool again.                                   ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                
                errors.Add("BalatroMobileCompat not found - REQUIRED for mobile mods");
                errors.Add($"Download from: https://github.com/eeve-lyn/BalatroMobileCompat");
                errors.Add($"Install to: {mobileCompatPath}");
                return CreateResult(false, null, messages, errors, DateTime.Now - startTime);
            }
            messages.Add("Found BalatroMobileCompat (required for mobile)");

            // Step 4c: Bundle Lovely dump files for mod support (HYBRID APPROACH)
            // This bundles the mod LOADER into game.love; actual Mods are transferred separately
            progress?.Report("Bundling mod loader (Lovely dump)...");
            var bundleResult = await BundleLovelyDumpAsync(extractPath, messages);
            if (bundleResult)
            {
                messages.Add("Bundled Lovely dump files into game");
            }
            else
            {
                messages.Add("Note: Lovely dump not found - building vanilla APK (run game with mods on PC first)");
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
            Console.WriteLine("=== STEP 3: Adding game.love to APK assets ===");
            var assetsPath = Path.Combine(decompiledPath, "assets");
            Console.WriteLine($"[BUILD] Assets path: {assetsPath}");
            Console.WriteLine($"[BUILD] Assets folder exists before: {Directory.Exists(assetsPath)}");
            
            Directory.CreateDirectory(assetsPath);
            Console.WriteLine($"[BUILD] Assets folder exists after create: {Directory.Exists(assetsPath)}");
            
            var targetGameLovePath = Path.Combine(assetsPath, "game.love");
            Console.WriteLine($"[BUILD] Source game.love: {gameLovePath}");
            Console.WriteLine($"[BUILD] Source exists: {File.Exists(gameLovePath)}");
            
            if (File.Exists(gameLovePath))
            {
                var sourceLoveInfo = new FileInfo(gameLovePath);
                Console.WriteLine($"[BUILD] Source game.love size: {sourceLoveInfo.Length / 1024.0 / 1024.0:F2} MB");
                
                File.Copy(gameLovePath, targetGameLovePath, true);
                
                Console.WriteLine($"[BUILD] Target game.love: {targetGameLovePath}");
                Console.WriteLine($"[BUILD] Target exists after copy: {File.Exists(targetGameLovePath)}");
                
                if (File.Exists(targetGameLovePath))
                {
                    var targetLoveInfo = new FileInfo(targetGameLovePath);
                    Console.WriteLine($"[BUILD] Target game.love size: {targetLoveInfo.Length / 1024.0 / 1024.0:F2} MB");
                    
                    if (sourceLoveInfo.Length != targetLoveInfo.Length)
                    {
                        Console.WriteLine($"[BUILD] WARNING: Size mismatch! Source={sourceLoveInfo.Length}, Target={targetLoveInfo.Length}");
                    }
                    else
                    {
                        Console.WriteLine($"[BUILD] Size verified OK");
                    }
                }
                else
                {
                    Console.WriteLine($"[BUILD] ERROR: Copy failed - target file not created!");
                    return null;
                }
            }
            else
            {
                Console.WriteLine($"[BUILD] ERROR: game.love not found at: {gameLovePath}");
                return null;
            }
            
            // List all files in assets folder
            Console.WriteLine($"[BUILD] Assets folder contents:");
            foreach (var f in Directory.GetFiles(assetsPath))
            {
                var info = new FileInfo(f);
                Console.WriteLine($"[BUILD]   - {Path.GetFileName(f)}: {info.Length / 1024.0 / 1024.0:F2} MB");
            }

            // Step 4: Apply Balatro APK patches if available
            Console.WriteLine("=== STEP 4: Applying Balatro APK patches ===");
            if (!string.IsNullOrEmpty(_balatroApkPatchPath) && Directory.Exists(_balatroApkPatchPath))
            {
                Console.WriteLine($"[BUILD] Patch source: {_balatroApkPatchPath}");
                await ApplyBalatroApkPatchesAsync(decompiledPath);
            }
            else
            {
                Console.WriteLine($"[BUILD] No Balatro APK patches to apply");
            }

            // Verify game.love still exists after patching
            Console.WriteLine($"[BUILD] Verifying game.love after patches...");
            Console.WriteLine($"[BUILD] game.love exists: {File.Exists(targetGameLovePath)}");
            if (File.Exists(targetGameLovePath))
            {
                var verifyInfo = new FileInfo(targetGameLovePath);
                Console.WriteLine($"[BUILD] game.love size: {verifyInfo.Length / 1024.0 / 1024.0:F2} MB");
            }

            // Step 5: Apply AndroidManifest patches
            Console.WriteLine("=== STEP 5: Patching AndroidManifest ===");
            await ApplyAndroidManifestPatchesAsync(decompiledPath);

            // Final verification before compile
            Console.WriteLine("=== PRE-COMPILE VERIFICATION ===");
            Console.WriteLine($"[BUILD] Decompiled path: {decompiledPath}");
            Console.WriteLine($"[BUILD] Decompiled exists: {Directory.Exists(decompiledPath)}");
            Console.WriteLine($"[BUILD] Assets folder: {assetsPath}");
            Console.WriteLine($"[BUILD] Assets exists: {Directory.Exists(assetsPath)}");
            Console.WriteLine($"[BUILD] game.love path: {targetGameLovePath}");
            Console.WriteLine($"[BUILD] game.love exists: {File.Exists(targetGameLovePath)}");
            if (File.Exists(targetGameLovePath))
            {
                var finalInfo = new FileInfo(targetGameLovePath);
                Console.WriteLine($"[BUILD] game.love final size: {finalInfo.Length / 1024.0 / 1024.0:F2} MB");
            }
            else
            {
                Console.WriteLine($"[BUILD] CRITICAL ERROR: game.love is missing before compile!");
                return null;
            }

            // Step 6: Recompile APK
            Console.WriteLine("=== STEP 6: Recompiling APK ===");
            var unsignedApkPath = Path.Combine(tempDir, "unsigned.apk");
            Console.WriteLine($"[BUILD] Unsigned APK path: {unsignedApkPath}");
            
            var recompileSuccess = await _apkTool.CompileAsync(decompiledPath, unsignedApkPath);
            if (!recompileSuccess)
            {
                Console.WriteLine("[BUILD] ERROR: Failed to recompile APK");
                return null;
            }
            
            if (File.Exists(unsignedApkPath))
            {
                var unsignedInfo = new FileInfo(unsignedApkPath);
                Console.WriteLine($"[BUILD] Recompiled APK: {unsignedInfo.Length / 1024.0 / 1024.0:F2} MB");
            }
            else
            {
                Console.WriteLine("[BUILD] ERROR: Unsigned APK was not created!");
                return null;
            }

            // Step 7: Sign APK
            Console.WriteLine("=== STEP 7: Signing APK ===");
            var signSuccess = await _apkTool.SignAsync(unsignedApkPath);
            
            // Look for signed APK (uber-apk-signer creates a new file)
            // uber-apk-signer naming: {input}-aligned-debugSigned.apk
            // Input: unsigned.apk -> Output: unsigned-aligned-debugSigned.apk
            var signedApkPath = Path.Combine(tempDir, "unsigned-aligned-debugSigned.apk");
            Console.WriteLine($"[BUILD] Looking for signed APK: {signedApkPath}");
            Console.WriteLine($"[BUILD] Exists: {File.Exists(signedApkPath)}");
            
            if (!File.Exists(signedApkPath))
            {
                // Try alternative naming patterns
                signedApkPath = Path.Combine(tempDir, "unsigned-debugSigned.apk");
                Console.WriteLine($"[BUILD] Trying alternative: {signedApkPath}");
                Console.WriteLine($"[BUILD] Exists: {File.Exists(signedApkPath)}");
            }
            if (!File.Exists(signedApkPath))
            {
                // List all APK files in temp directory
                Console.WriteLine($"[BUILD] All APK files in {tempDir}:");
                foreach (var f in Directory.GetFiles(tempDir, "*.apk"))
                {
                    Console.WriteLine($"[BUILD]   - {Path.GetFileName(f)}");
                }
                
                // Use unsigned APK if signing failed
                signedApkPath = unsignedApkPath;
                Console.WriteLine("[BUILD] WARNING: Signed APK not found! Using unsigned APK");
            }
            else
            {
                var signedInfo = new FileInfo(signedApkPath);
                Console.WriteLine($"[BUILD] Using signed APK: {Path.GetFileName(signedApkPath)} ({signedInfo.Length / 1024.0 / 1024.0:F2} MB)");
            }

            // Copy to final output location
            Console.WriteLine("=== STEP 8: Copying to output ===");
            Console.WriteLine($"[BUILD] Source: {signedApkPath}");
            Console.WriteLine($"[BUILD] Target: {outputPath}");
            File.Copy(signedApkPath, outputPath, true);
            
            if (File.Exists(outputPath))
            {
                var finalInfo = new FileInfo(outputPath);
                Console.WriteLine($"[BUILD] Final APK: {outputPath} ({finalInfo.Length / 1024.0 / 1024.0:F2} MB)");
            }
            else
            {
                Console.WriteLine("[BUILD] ERROR: Final APK copy failed!");
                return null;
            }

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
            
            Console.WriteLine($"Applied Balatro APK patches: {string.Join(", ", filesCopied)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to apply Balatro APK patches: {ex.Message}");
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
            
            // IMPORTANT: Only change the package ATTRIBUTE, not all org.love2d.android references!
            // The Activity class is still org.love2d.android.GameActivity - don't change that
            content = content.Replace("package=\"org.love2d.android\"", "package=\"com.unofficial.balatro\"");
            
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

        // CRITICAL: t.identity patch - ensures predictable save directory path
        // Without this, LÖVE uses default path which breaks mod loading
        // With t.identity = "Balatro", save path becomes save/Balatro/
        patches.Add(new PatchConfig
        {
            FilePath = "conf.lua",
            SearchPattern = "t.console = not _RELEASE_MODE",
            Replacement = "    t.identity = \"Balatro\"\n    t.console = not _RELEASE_MODE",
            Description = "Set save directory identity (CRITICAL for mods)"
        });

        return patches;
    }

    /// <summary>
    /// Bundles Lovely dump files into the extracted game for mod support.
    /// This is the HYBRID APPROACH: mod loader in game.love, Mods transferred separately.
    /// </summary>
    private async Task<bool> BundleLovelyDumpAsync(string extractPath, List<string> messages)
    {
        try
        {
            Console.WriteLine("=== BUNDLE LOVELY DUMP ===");
            Console.WriteLine($"[BUNDLE] Extract path: {extractPath}");
            Console.WriteLine($"[BUNDLE] Extract path exists: {Directory.Exists(extractPath)}");
            
            // Find Balatro AppData with Lovely dump
            var balatroAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Balatro"
            );
            
            Console.WriteLine($"[BUNDLE] Balatro AppData: {balatroAppData}");
            Console.WriteLine($"[BUNDLE] AppData exists: {Directory.Exists(balatroAppData)}");
            
            var modsPath = Path.Combine(balatroAppData, "Mods");
            var lovelyDumpPath = Path.Combine(modsPath, "lovely", "dump");
            
            Console.WriteLine($"[BUNDLE] Mods path: {modsPath}");
            Console.WriteLine($"[BUNDLE] Mods exists: {Directory.Exists(modsPath)}");
            Console.WriteLine($"[BUNDLE] Lovely dump path: {lovelyDumpPath}");
            Console.WriteLine($"[BUNDLE] Lovely dump exists: {Directory.Exists(lovelyDumpPath)}");
            
            if (!Directory.Exists(lovelyDumpPath))
            {
                Console.WriteLine($"[BUNDLE] ERROR: Lovely dump not found at: {lovelyDumpPath}");
                return false;
            }
            
            // List lovely dump contents
            Console.WriteLine($"[BUNDLE] Lovely dump contents:");
            foreach (var item in Directory.GetFileSystemEntries(lovelyDumpPath))
            {
                var isDir = Directory.Exists(item);
                Console.WriteLine($"[BUNDLE]   - {Path.GetFileName(item)} {(isDir ? "(DIR)" : "")}");
            }
            
            // 1. Copy Lovely dump files (overlaying extracted Balatro)
            Console.WriteLine($"[BUNDLE] Step 1: Copying Lovely dump files...");
            int filesCopied = 0;
            int dirsCopied = 0;
            foreach (var item in Directory.GetFileSystemEntries(lovelyDumpPath))
            {
                var destPath = Path.Combine(extractPath, Path.GetFileName(item));
                if (Directory.Exists(item))
                {
                    await CopyDirectoryAsync(item, destPath);
                    dirsCopied++;
                }
                else
                {
                    File.Copy(item, destPath, true);
                    filesCopied++;
                }
            }
            Console.WriteLine($"[BUNDLE] Copied {filesCopied} files, {dirsCopied} directories");
            messages.Add("Overlaid Lovely dump files");
            
            // 2. Create SMODS folder with version.lua and release.lua
            Console.WriteLine($"[BUNDLE] Step 2: Creating SMODS folder...");
            var smodsLibPath = Path.Combine(modsPath, "smods");
            var smodsDestPath = Path.Combine(extractPath, "SMODS");
            Console.WriteLine($"[BUNDLE] SMODS source: {smodsLibPath}");
            Console.WriteLine($"[BUNDLE] SMODS source exists: {Directory.Exists(smodsLibPath)}");
            
            Directory.CreateDirectory(smodsDestPath);
            Console.WriteLine($"[BUNDLE] SMODS dest created: {smodsDestPath}");
            
            var versionLuaPath = Path.Combine(smodsLibPath, "version.lua");
            Console.WriteLine($"[BUNDLE] version.lua exists: {File.Exists(versionLuaPath)}");
            if (File.Exists(versionLuaPath))
            {
                File.Copy(versionLuaPath, Path.Combine(smodsDestPath, "version.lua"), true);
                Console.WriteLine($"[BUNDLE] Copied version.lua");
            }
            
            var releaseLuaPath = Path.Combine(smodsLibPath, "release.lua");
            Console.WriteLine($"[BUNDLE] release.lua exists: {File.Exists(releaseLuaPath)}");
            if (File.Exists(releaseLuaPath))
            {
                File.Copy(releaseLuaPath, Path.Combine(smodsDestPath, "release.lua"), true);
                Console.WriteLine($"[BUNDLE] Copied release.lua");
            }
            
            // 3. Create nativefs stub (replaces FFI version that doesn't work on Android)
            var nativefsStub = @"-- NativeFS stub for Android - WORKING VERSION
local nfs = {}
local lf = love.filesystem
local _workingDir = ''
function nfs.read(arg1, arg2, arg3)
    local container, name, size
    if arg3 ~= nil then container, name, size = arg1, arg2, arg3
    elseif arg2 == nil then container, name, size = 'string', arg1, 'all'
    else
        if type(arg2) == 'number' or arg2 == 'all' then container, name, size = 'string', arg1, arg2
        else container, name, size = arg1, arg2, 'all' end
    end
    local contents, bytes = lf.read(name)
    if not contents then return nil, 0 end
    if container == 'data' then
        local filename = name:match('[^/]+$') or name
        return lf.newFileData(contents, filename), bytes
    end
    return contents, bytes
end
function nfs.load(path) return lf.load(path) end
function nfs.getDirectoryItems(dir) return lf.getDirectoryItems(dir) or {} end
function nfs.getDirectoryItemsInfo(dir, ft)
    local items = nfs.getDirectoryItems(dir)
    local r = {}
    for _, i in ipairs(items) do
        local p = dir..'/'..i
        local inf = lf.getInfo(p)
        if inf and (not ft or inf.type == ft) then inf.name = i; r[#r+1] = inf end
    end
    return r
end
function nfs.getInfo(path) return lf.getInfo(path) end
function nfs.setWorkingDirectory(d) _workingDir = d or ''; return true end
function nfs.getWorkingDirectory() return _workingDir end
function nfs.write(p, d, s) return lf.write(p, d, s) end
function nfs.append(p, d, s) return lf.append(p, d, s) end
function nfs.createDirectory(p) return lf.createDirectory(p) end
function nfs.remove(p) return lf.remove(p) end
function nfs.getSaveDirectory() return lf.getSaveDirectory() end
function nfs.getSourceBaseDirectory() return '' end
function nfs.isFile(p) local i = lf.getInfo(p); return i and i.type == 'file' end
function nfs.isDirectory(p) local i = lf.getInfo(p); return i and i.type == 'directory' end
function nfs.lines(p) return lf.lines(p) end
function nfs.newFile(p, m) return lf.newFile(p, m) end
function nfs.newFileData(a1, a2)
    if a2 then return lf.newFileData(a1, a2)
    else
        local c = lf.read(a1)
        if not c then return nil end
        return lf.newFileData(c, a1:match('[^/]+$') or a1)
    end
end
return nfs";
            
            // 3. Create nativefs.lua at root and nativefs/init.lua
            Console.WriteLine($"[BUNDLE] Step 3: Creating nativefs stubs...");
            var nativefsDir = Path.Combine(extractPath, "nativefs");
            Directory.CreateDirectory(nativefsDir);
            await File.WriteAllTextAsync(Path.Combine(nativefsDir, "init.lua"), nativefsStub);
            await File.WriteAllTextAsync(Path.Combine(extractPath, "nativefs.lua"), nativefsStub);
            Console.WriteLine($"[BUNDLE] Created nativefs/init.lua and nativefs.lua");
            
            // 4. Create lovely stub
            Console.WriteLine($"[BUNDLE] Step 4: Creating lovely stub...");
            var lovelyStub = @"local lovely = {}
lovely.mod_dir = 'Mods'
lovely.path = 'Mods'
lovely.version = '0.6.0'
return lovely";
            
            var lovelyDir = Path.Combine(extractPath, "lovely");
            Directory.CreateDirectory(lovelyDir);
            await File.WriteAllTextAsync(Path.Combine(lovelyDir, "init.lua"), lovelyStub);
            Console.WriteLine($"[BUNDLE] Created lovely/init.lua");
            
            // 5. Copy json.lua from smods
            Console.WriteLine($"[BUNDLE] Step 5: Copying json.lua...");
            var jsonLuaPath = Path.Combine(smodsLibPath, "libs", "json", "json.lua");
            Console.WriteLine($"[BUNDLE] json.lua source: {jsonLuaPath}");
            Console.WriteLine($"[BUNDLE] json.lua exists: {File.Exists(jsonLuaPath)}");
            if (File.Exists(jsonLuaPath))
            {
                File.Copy(jsonLuaPath, Path.Combine(extractPath, "json.lua"), true);
                Console.WriteLine($"[BUNDLE] Copied json.lua");
            }
            else
            {
                Console.WriteLine($"[BUNDLE] WARNING: json.lua not found!");
            }
            
            // 6. Clean macOS metadata files
            Console.WriteLine($"[BUNDLE] Step 6: Cleaning macOS metadata...");
            await CleanMacOsMetadataAsync(extractPath);
            
            // Final verification - list key files
            Console.WriteLine($"[BUNDLE] Final verification - Key files in extract path:");
            var keyFiles = new[] { "main.lua", "conf.lua", "nativefs.lua", "json.lua" };
            foreach (var kf in keyFiles)
            {
                var path = Path.Combine(extractPath, kf);
                Console.WriteLine($"[BUNDLE]   - {kf}: {(File.Exists(path) ? "EXISTS" : "MISSING")}");
            }
            var keyDirs = new[] { "nativefs", "lovely", "SMODS", "engine", "functions" };
            foreach (var kd in keyDirs)
            {
                var path = Path.Combine(extractPath, kd);
                Console.WriteLine($"[BUNDLE]   - {kd}/: {(Directory.Exists(path) ? "EXISTS" : "MISSING")}");
            }
            
            Console.WriteLine("[BUNDLE] Bundled mod loader components successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Failed to bundle Lovely dump: {ex.Message}");
            return false;
        }
    }

    private async Task CopyDirectoryAsync(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destSubDir);
        }
    }

    private async Task CleanMacOsMetadataAsync(string directory)
    {
        var metadataFiles = Directory.GetFiles(directory, "._*", SearchOption.AllDirectories);
        foreach (var file in metadataFiles)
        {
            try { File.Delete(file); } catch { }
        }
        
        var dsStoreFiles = Directory.GetFiles(directory, ".DS_Store", SearchOption.AllDirectories);
        foreach (var file in dsStoreFiles)
        {
            try { File.Delete(file); } catch { }
        }
        
        await Task.CompletedTask;
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
