using BalatroMobile.Configuration.Services;
using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services;
using BalatroMobile.Core.Services.GameDetection;
using BalatroMobile.Infrastructure.Tools;

namespace BalatroMobile.Cli;

internal class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                // Interactive mode when no arguments provided
                await RunInteractiveMode();
                return;
            }
            else if (args[0] == "check")
            {
                await RunPreFlightChecks();
            }
            else if (args[0] == "build")
            {
                await RunBuild(args.Skip(1).ToArray());
            }
            else if (args[0] == "transfer")
            {
                await RunTransfer(args.Skip(1).ToArray());
            }
            else if (args[0] == "--help" || args[0] == "-h")
            {
                ShowUsage();
            }
            else
            {
                ShowUsage();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Run 'BalatroMobile --help' for usage information.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(1);
        }
    }

    private static async Task RunBuild(string[] args)
    {
        try
        {
            // Parse build arguments
            var platform = Platform.Android; // Default
            var fpsCap = FpsCap.Default;
            var enableLandscape = true;
            var enableHighDpi = false;
            var disableCrtShader = false;
            var injectMods = false;
            var outputPath = "balatro.apk";

            // Simple argument parsing (could be improved with a proper CLI library)
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--platform":
                        if (i + 1 < args.Length)
                        {
                            platform = Enum.Parse<Platform>(args[i + 1], true);
                            i++;
                        }
                        break;
                    case "--fps":
                        if (i + 1 < args.Length)
                        {
                            if (args[i + 1] == "default")
                                fpsCap = FpsCap.Default;
                            else if (args[i + 1] == "none")
                                fpsCap = FpsCap.None;
                            else
                            {
                                fpsCap = FpsCap.Custom;
                                // Custom FPS value would be stored here
                            }
                            i++;
                        }
                        break;
                    case "--no-landscape":
                        enableLandscape = false;
                        break;
                    case "--high-dpi":
                        enableHighDpi = true;
                        break;
                    case "--disable-crt":
                        disableCrtShader = true;
                        break;
                    case "--inject-mods":
                        injectMods = true;
                        break;
                    case "--output":
                        if (i + 1 < args.Length)
                        {
                            outputPath = args[i + 1];
                            i++;
                        }
                        break;
                }
            }

            var config = new BuildConfig
            {
                Platform = platform,
                FpsCap = fpsCap,
                EnableLandscape = enableLandscape,
                EnableHighDpi = enableHighDpi,
                DisableCrtShader = disableCrtShader,
                InjectMods = injectMods,
                OutputPath = outputPath
            };

            Console.WriteLine("BalatroMobile Build");
            Console.WriteLine("======================");
            Console.WriteLine();
            Console.WriteLine($"Platform: {config.Platform}");
            Console.WriteLine($"FPS Cap: {config.FpsCap}");
            Console.WriteLine($"Landscape: {config.EnableLandscape}");
            Console.WriteLine($"High DPI: {config.EnableHighDpi}");
            Console.WriteLine($"Disable CRT: {config.DisableCrtShader}");
            Console.WriteLine($"Inject Mods: {config.InjectMods}");
            Console.WriteLine($"Output: {config.OutputPath}");
            Console.WriteLine();

            // Create services (in real implementation, this would use dependency injection)
            var gameDetector = new GameDetector();
            var patchService = new PatchService();
            var modInjectionService = new ModInjectionService();
            var javaTool = new JavaTool();
            var apkTool = new ApkTool(javaTool, "apktool.jar"); // Placeholder path
            var buildService = new BuildService(gameDetector, patchService, modInjectionService, apkTool, javaTool);

            // Validate environment first
            Console.WriteLine("Validating build environment...");
            if (!await buildService.ValidateBuildEnvironmentAsync())
            {
                Console.WriteLine("ERROR: Build environment validation failed!");
                Console.WriteLine("Please run 'BalatroMobile check' to see what needs to be fixed.");
                return;
            }
            Console.WriteLine("OK: Build environment validated");

            // Progress reporting
            var progress = new Progress<string>(message => Console.WriteLine($"📋 {message}"));

            Console.WriteLine("Starting build process...");
            var result = await buildService.BuildAsync(config, progress);

            Console.WriteLine();
            Console.WriteLine("Build completed!");
            Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
            Console.WriteLine();

            if (result.Success)
            {
                Console.WriteLine("SUCCESS: Build completed!");
                if (!string.IsNullOrEmpty(result.OutputPath))
                {
                    Console.WriteLine($"📱 Output: {result.OutputPath}");
                }

                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine("1. Transfer the APK to your Android device");
                Console.WriteLine("2. Install and run the app");
                Console.WriteLine("3. Run 'BalatroMobile transfer' to copy your saves");
            }
            else
            {
                Console.WriteLine("ERROR: Build failed!");
                Console.WriteLine();
                Console.WriteLine("Errors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

            if (result.Messages.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Build log:");
                foreach (var message in result.Messages)
                {
                    Console.WriteLine($"  ✓ {message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Build failed with exception: {ex.Message}");
        }
    }

    private static async Task RunTransfer(string[] args)
    {
        try
        {
            // Parse transfer arguments
            var direction = TransferDirection.PcToAndroid; // Default
            var createBackup = true;
            var includeMods = true;

            // Simple argument parsing
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--from":
                        if (i + 1 < args.Length)
                        {
                            direction = args[i + 1].ToLower() switch
                            {
                                "android" => TransferDirection.AndroidToPc,
                                "pc" or _ => TransferDirection.PcToAndroid
                            };
                            i++;
                        }
                        break;
                    case "--to":
                        if (i + 1 < args.Length)
                        {
                            direction = args[i + 1].ToLower() switch
                            {
                                "pc" => TransferDirection.AndroidToPc,
                                "android" or _ => TransferDirection.PcToAndroid
                            };
                            i++;
                        }
                        break;
                    case "--no-backup":
                        createBackup = false;
                        break;
                    case "--no-mods":
                        includeMods = false;
                        break;
                }
            }

            var config = new SaveTransferConfig
            {
                Direction = direction,
                CreateBackup = createBackup,
                IncludeMods = includeMods
            };

            Console.WriteLine("BalatroMobile Save Transfer");
            Console.WriteLine("===========================");
            Console.WriteLine();
            Console.WriteLine($"Direction: {config.Direction}");
            Console.WriteLine($"Create Backup: {config.CreateBackup}");
            Console.WriteLine($"Include Mods: {config.IncludeMods}");
            Console.WriteLine();

            // Create services
            var platformDetector = new PlatformDetector();
            var saveTransferService = new SaveTransferService(platformDetector);

            // Validate environment
            Console.WriteLine("Validating transfer environment...");
            if (!await saveTransferService.ValidateTransferEnvironmentAsync(config))
            {
                Console.WriteLine("ERROR: Transfer environment validation failed!");
                Console.WriteLine("Please ensure:");
                Console.WriteLine("- Android device is connected via USB");
                Console.WriteLine("- USB debugging is enabled");
                Console.WriteLine("- ADB can communicate with the device");
                return;
            }
            Console.WriteLine("OK: Transfer environment validated");

            // Show what will be transferred
            var availableFiles = await saveTransferService.GetAvailableSaveFilesAsync();
            Console.WriteLine($"Found save files: {string.Join(", ", availableFiles)}");

            // Confirm transfer
            Console.WriteLine();
            Console.WriteLine($"Ready to transfer saves from {config.Direction}.");
            if (config.CreateBackup)
            {
                Console.WriteLine("A backup will be created before transfer.");
            }
            Console.WriteLine();
            Console.WriteLine("WARNING: This will overwrite existing save files!");
            Console.WriteLine("Continue? (y/N): ");

            var response = Console.ReadLine()?.ToLower();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Transfer cancelled.");
                return;
            }

            // Perform transfer
            Console.WriteLine("Starting transfer...");
            var result = await saveTransferService.TransferSavesAsync(config);

            Console.WriteLine();
            if (result.Success)
            {
                Console.WriteLine("SUCCESS: Transfer completed!");
                Console.WriteLine($"Files transferred: {result.FilesTransferred}");
                Console.WriteLine($"Data transferred: {result.BytesTransferred} bytes");
                Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");

                if (!string.IsNullOrEmpty(result.BackupPath))
                {
                    Console.WriteLine($"Backup created: {result.BackupPath}");
                }

                if (result.TransferredFiles.Any())
                {
                    Console.WriteLine("Transferred files:");
                    foreach (var file in result.TransferredFiles)
                    {
                        Console.WriteLine($"  ✓ {file}");
                    }
                }
            }
            else
            {
                Console.WriteLine("ERROR: Transfer failed!");
                Console.WriteLine("Errors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Transfer failed with exception: {ex.Message}");
        }
    }

    private static async Task RunInteractiveMode()
    {
        Console.WriteLine("=====================================");
        Console.WriteLine("    BalatroMobile Interactive Mode");
        Console.WriteLine("=====================================");
        Console.WriteLine();

        // Initial setup questions (like original)
        bool cleanupAfter = AskYesNo("Would you like to automatically clean up once complete?", false);
        bool verboseMode = AskYesNo("Would you like to enable extra logging information?", false);

        // Check for existing builds
        string existingApk = "balatro.apk";
        string existingIpa = "balatro.ipa";
        bool hasExistingBuild = File.Exists(existingApk) || File.Exists(existingIpa);

        if (hasExistingBuild)
        {
            Console.WriteLine($"Found existing build: {(File.Exists(existingApk) ? existingApk : existingIpa)}");
            bool rebuild = AskYesNo("Would you like to build again?", true);
            if (!rebuild)
            {
                Console.WriteLine("Build cancelled.");
                WaitForKeyPress();
                return;
            }
        }

        // Platform selection
        bool buildAndroid = AskYesNo("Would you like to build for Android?", true);
        bool buildIos = AskYesNo("Would you like to build for iOS (experimental)?", false);

        if (!buildAndroid && !buildIos)
        {
            Console.WriteLine("No platform selected. Exiting...");
            WaitForKeyPress();
            return;
        }

        // Run pre-flight checks
        Console.WriteLine();
        await RunPreFlightChecks();
        Console.WriteLine();

        // Configure build options interactively (like original patching questions)
        var buildArgs = new List<string>();

        // Set platform
        if (!buildAndroid && buildIos)
        {
            buildArgs.Add("--platform");
            buildArgs.Add("ios");
        }

        // FPS patch question (like original)
        bool applyFpsPatch = AskYesNo("Would you like to apply the FPS cap patch?", true);
        if (applyFpsPatch)
        {
            Console.WriteLine();
            Console.WriteLine("FPS options:");
            Console.WriteLine("1. Default (recommended)");
            Console.WriteLine("2. 60 FPS");
            Console.WriteLine("3. No limit");
            int fpsChoice = AskChoice("Choose FPS setting", 1, 3);
            if (fpsChoice == 2)
            {
                buildArgs.Add("--fps");
                buildArgs.Add("60");
            }
            else if (fpsChoice == 3)
            {
                buildArgs.Add("--fps");
                buildArgs.Add("none");
            }
        }

        // Landscape orientation patch (like original)
        bool applyLandscapePatch = AskYesNo("Would you like to apply the landscape orientation patch?", true);
        if (applyLandscapePatch)
        {
            buildArgs.Add("--no-landscape");
        }

        // High DPI patch (like original)
        bool applyHighDpiPatch = AskYesNo("Would you like to apply the high DPI patch (recommended for devices with high resolution)?", true);
        if (applyHighDpiPatch)
        {
            buildArgs.Add("--high-dpi");
        }

        // CRT shader disable patch (like original)
        bool applyCrtPatch = AskYesNo("Would you like to apply the CRT shader disable patch? (Required for Pixel and some other devices!)", false);
        if (applyCrtPatch)
        {
            buildArgs.Add("--disable-crt");
        }

        // Mod injection (our addition)
        bool injectMods = AskYesNo("Include mods (Cryptid, Talisman, etc.)?", true);
        if (injectMods)
        {
            buildArgs.Add("--inject-mods");
        }

        // Output file
        Console.WriteLine();
        Console.Write("Output filename (press Enter for default): ");
        string outputFile = Console.ReadLine()?.Trim() ?? "";
        if (!string.IsNullOrEmpty(outputFile))
        {
            buildArgs.Add("--output");
            buildArgs.Add(outputFile);
        }

        // Show configuration summary
        Console.WriteLine();
        Console.WriteLine("Build configuration:");
        Console.WriteLine($"Platform: {(buildAndroid ? "Android" : "iOS")}");
        Console.WriteLine($"FPS Patch: {(applyFpsPatch ? "Yes" : "No")}");
        Console.WriteLine($"Landscape Patch: {(applyLandscapePatch ? "Yes" : "No")}");
        Console.WriteLine($"High DPI Patch: {(applyHighDpiPatch ? "Yes" : "No")}");
        Console.WriteLine($"CRT Shader Patch: {(applyCrtPatch ? "Yes" : "No")}");
        Console.WriteLine($"Mods: {(injectMods ? "Yes" : "No")}");
        Console.WriteLine($"Cleanup: {(cleanupAfter ? "Yes" : "No")}");
        Console.WriteLine($"Verbose: {(verboseMode ? "Yes" : "No")}");

        bool confirm = AskYesNo("Start build with these settings?", true);
        if (!confirm)
        {
            Console.WriteLine("Build cancelled.");
            WaitForKeyPress();
            return;
        }

        // Run the build
        Console.WriteLine();
        await RunBuild(buildArgs.ToArray());

        // Post-build options (like original)
        if (buildAndroid && File.Exists("balatro.apk"))
        {
            Console.WriteLine();
            bool autoInstall = AskYesNo("Would you like to automatically install balatro.apk on your Android device?", false);
            if (autoInstall)
            {
                Console.WriteLine("Auto-install not yet implemented in this version.");
                // TODO: Implement auto-install
            }

            // Save transfer options (like original)
            bool transferSaves = AskYesNo("Would you like to transfer saves from your Steam copy of Balatro to your Android device?", false);
            if (transferSaves)
            {
                Console.WriteLine("Thanks for using BalatroMobile!");
                await RunTransfer(new[] { "--from", "pc", "--to", "android" });
            }
            else
            {
                bool pullSaves = AskYesNo("Would you like to pull saves from your Android device?", false);
                if (pullSaves)
                {
                    Console.WriteLine("Warning! This will overwrite your PC saves!");
                    bool backupConfirmed = AskYesNo("Have you backed up your PC saves?", false);
                    if (backupConfirmed)
                    {
                        await RunTransfer(new[] { "--from", "android", "--to", "pc" });
                    }
                    else
                    {
                        Console.WriteLine("Please back up your saves first!");
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Finished!");
        WaitForKeyPress();
    }

    private static bool AskYesNo(string question, bool defaultYes = false)
    {
        string defaultText = defaultYes ? "Y/n" : "y/N";
        while (true)
        {
            Console.Write($"{question} ({defaultText}): ");
            string? response = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(response))
            {
                return defaultYes;
            }
            if (response == "y" || response == "yes")
            {
                return true;
            }
            if (response == "n" || response == "no")
            {
                return false;
            }
            Console.WriteLine("Please answer 'y' or 'n'.");
        }
    }

    private static int AskChoice(string question, int min, int max)
    {
        while (true)
        {
            Console.Write($"{question} ({min}-{max}): ");
            string? response = Console.ReadLine()?.Trim();
            if (int.TryParse(response, out int choice) && choice >= min && choice <= max)
            {
                return choice;
            }
            Console.WriteLine($"Please enter a number between {min} and {max}.");
        }
    }

    private static void WaitForKeyPress()
    {
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    private static void ShowUsage()
    {
        Console.WriteLine("BalatroMobile - Build Balatro for Mobile Devices");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  BalatroMobile                        - Interactive mode (recommended)");
        Console.WriteLine("  BalatroMobile check                  - Run pre-flight checks");
        Console.WriteLine("  BalatroMobile build [options]        - Build for mobile");
        Console.WriteLine("  BalatroMobile transfer [options]     - Transfer saves between PC and Android");
        Console.WriteLine();
        Console.WriteLine("Build Options:");
        Console.WriteLine("  --platform <android|ios>            - Target platform (default: android)");
        Console.WriteLine("  --fps <default|none|60>             - FPS cap (default: default)");
        Console.WriteLine("  --no-landscape                       - Disable landscape lock");
        Console.WriteLine("  --high-dpi                           - Enable high DPI mode");
        Console.WriteLine("  --disable-crt                        - Disable CRT shader");
        Console.WriteLine("  --inject-mods                        - Inject mods during build");
        Console.WriteLine("  --output <path>                      - Output file path");
        Console.WriteLine();
        Console.WriteLine("Transfer Options:");
        Console.WriteLine("  --from <pc|android>                  - Source platform (default: pc)");
        Console.WriteLine("  --to <android|pc>                    - Target platform (default: android)");
        Console.WriteLine("  --no-backup                          - Skip backup creation");
        Console.WriteLine("  --no-mods                            - Skip mod-related files");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  BalatroMobile transfer");
        Console.WriteLine("  BalatroMobile transfer --from android --to pc");
        Console.WriteLine("  BalatroMobile transfer --no-backup");
        Console.WriteLine();
        WaitForKeyPress();
    }

    private static async Task RunPreFlightChecks()
    {
        Console.WriteLine("BalatroMobile Pre-Flight Check");
        Console.WriteLine("===============================");
        Console.WriteLine();

        // For now, create a simple implementation
        // In a real implementation, this would use dependency injection
        var checker = new PreFlightCheckService(
            new GameDetector(),
            new PlatformDetector()
        );

        Console.WriteLine("Running system checks...");
        Console.WriteLine();

        var result = await checker.RunAllChecksAsync();

        foreach (var checkResult in result.Results)
        {
            var icon = checkResult.Result switch
            {
                Configuration.Models.CheckResult.Pass => "[OK]",
                Configuration.Models.CheckResult.Fail => "[FAIL]",
                Configuration.Models.CheckResult.Warning => "[WARN]",
                _ => "❓"
            };

            Console.WriteLine($"{icon} {checkResult.CheckName}");
            Console.WriteLine($"   {checkResult.Message}");

            if (!string.IsNullOrEmpty(checkResult.FixSuggestion) &&
                checkResult.Result != Configuration.Models.CheckResult.Pass)
            {
                Console.WriteLine($"   💡 {checkResult.FixSuggestion}");
            }

            Console.WriteLine();
        }

        if (result.Errors.Any())
        {
            Console.WriteLine("🚨 Errors encountered:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"   - {error}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("=================================");

        if (result.AllPassed)
        {
            Console.WriteLine("SUCCESS: All checks passed! Ready to build.");
            Console.WriteLine("Run 'BalatroMobile build' to start building.");
        }
        else
        {
            Console.WriteLine("WARNING: Some checks failed. Please fix the issues above before building.");
            Console.WriteLine("Run 'BalatroMobile check' again after fixing issues.");
        }
    }
}

// Placeholder implementations - these would be properly implemented
internal class GameDetector : IGameDetector
{
    public Task<bool> IsSteamBalatroInstalledAsync() => Task.FromResult(true);
    public Task<string?> GetGameInstallPathAsync() => Task.FromResult<string?>("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Balatro");
    public Task<bool> IsGameWorkingAsync() => Task.FromResult(true);
}

internal class PlatformDetector : IPlatformDetector
{
    public Task<bool> AreAndroidDeveloperOptionsEnabledAsync() => Task.FromResult(false);
    public Task<bool> IsUSBDebuggingEnabledAsync() => Task.FromResult(false);
    public Task<bool> IsADBConnectionWorkingAsync() => Task.FromResult(true);
    public Task<bool> HasAndroidSufficientStorageAsync() => Task.FromResult(true);
    public Task<bool> IsJavaRuntimeAvailableAsync() => Task.FromResult(true);
    public Task<bool> IsInternetConnectionAvailableAsync() => Task.FromResult(true);
}