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
        if (args.Length == 0 || args[0] == "check")
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
        else
        {
            ShowUsage();
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

            Console.WriteLine("🛠️  BalatroMobile Build");
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
                Console.WriteLine("❌ Build environment validation failed!");
                Console.WriteLine("Please run 'BalatroMobile check' to see what needs to be fixed.");
                return;
            }
            Console.WriteLine("✅ Build environment validated");

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
                Console.WriteLine("🎉 Build successful!");
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
                Console.WriteLine("❌ Build failed!");
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
            Console.WriteLine($"❌ Build failed with exception: {ex.Message}");
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

            Console.WriteLine("💾 BalatroMobile Save Transfer");
            Console.WriteLine("=============================");
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
                Console.WriteLine("❌ Transfer environment validation failed!");
                Console.WriteLine("Please ensure:");
                Console.WriteLine("- Android device is connected via USB");
                Console.WriteLine("- USB debugging is enabled");
                Console.WriteLine("- ADB can communicate with the device");
                return;
            }
            Console.WriteLine("✅ Transfer environment validated");

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
            Console.WriteLine("⚠️  WARNING: This will overwrite existing save files!");
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
                Console.WriteLine("🎉 Transfer completed successfully!");
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
                Console.WriteLine("❌ Transfer failed!");
                Console.WriteLine("Errors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Transfer failed with exception: {ex.Message}");
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("BalatroMobile - Build Balatro for Mobile Devices");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  BalatroMobile check                    - Run pre-flight checks");
        Console.WriteLine("  BalatroMobile build [options]          - Build for mobile");
        Console.WriteLine("  BalatroMobile transfer [options]       - Transfer saves between PC and Android");
        Console.WriteLine();
        Console.WriteLine("Build Options:");
        Console.WriteLine("  --platform <android|ios>              - Target platform (default: android)");
        Console.WriteLine("  --fps <default|none|60>               - FPS cap (default: default)");
        Console.WriteLine("  --no-landscape                         - Disable landscape lock");
        Console.WriteLine("  --high-dpi                             - Enable high DPI mode");
        Console.WriteLine("  --disable-crt                          - Disable CRT shader");
        Console.WriteLine("  --inject-mods                          - Inject mods during build");
        Console.WriteLine("  --output <path>                        - Output file path");
        Console.WriteLine();
        Console.WriteLine("Transfer Options:");
        Console.WriteLine("  --from <pc|android>                    - Source platform (default: pc)");
        Console.WriteLine("  --to <android|pc>                      - Target platform (default: android)");
        Console.WriteLine("  --no-backup                            - Skip backup creation");
        Console.WriteLine("  --no-mods                              - Skip mod-related files");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  BalatroMobile transfer");
        Console.WriteLine("  BalatroMobile transfer --from android --to pc");
        Console.WriteLine("  BalatroMobile transfer --no-backup");
        Console.WriteLine();
        Console.WriteLine("Build Options:");
        Console.WriteLine("  --platform <android|ios>              - Target platform (default: android)");
        Console.WriteLine("  --fps <default|none|60>               - FPS cap (default: default)");
        Console.WriteLine("  --no-landscape                         - Disable landscape lock");
        Console.WriteLine("  --high-dpi                             - Enable high DPI mode");
        Console.WriteLine("  --disable-crt                          - Disable CRT shader");
        Console.WriteLine("  --inject-mods                          - Inject mods during build");
        Console.WriteLine("  --output <path>                        - Output file path");
        Console.WriteLine();
        Console.WriteLine("Transfer Options:");
        Console.WriteLine("  --from <pc|android>                    - Source platform (default: pc)");
        Console.WriteLine("  --to <android|pc>                      - Target platform (default: android)");
        Console.WriteLine("  --no-backup                            - Skip backup creation");
        Console.WriteLine("  --no-mods                              - Skip mod-related files");
    }

    private static async Task RunPreFlightChecks()
    {
        Console.WriteLine("🃏 BalatroMobile Pre-Flight Check");
        Console.WriteLine("=================================");
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
                Configuration.Models.CheckResult.Pass => "✅",
                Configuration.Models.CheckResult.Fail => "❌",
                Configuration.Models.CheckResult.Warning => "⚠️",
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
            Console.WriteLine("🎉 All checks passed! Ready to build.");
            Console.WriteLine("Run 'BalatroMobile build' to start building.");
        }
        else
        {
            Console.WriteLine("⚠️ Some checks failed. Please fix the issues above before building.");
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