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
            Console.WriteLine("🚧 Transfer functionality coming soon!");
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

    private static void ShowUsage()
    {
        Console.WriteLine("BalatroMobile - Build Balatro for Mobile Devices");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  BalatroMobile check                    - Run pre-flight checks");
        Console.WriteLine("  BalatroMobile build [options]          - Build for mobile");
        Console.WriteLine("  BalatroMobile transfer                 - Transfer saves (coming soon)");
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
        Console.WriteLine("Examples:");
        Console.WriteLine("  BalatroMobile build");
        Console.WriteLine("  BalatroMobile build --platform android --fps 60 --high-dpi");
        Console.WriteLine("  BalatroMobile build --no-landscape --output my-balatro.apk");
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