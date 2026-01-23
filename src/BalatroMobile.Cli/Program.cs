using BalatroMobile.Configuration.Services;
using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services;
using BalatroMobile.Core.Services.GameDetection;
using BalatroMobile.Infrastructure.Logging;
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
            else if (args[0] == "tools")
            {
                await RunToolsCommand(args.Skip(1).ToArray());
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

            // Initialize comprehensive logger
            using var logger = new BuildLogger();
            var buildStartTime = DateTime.Now;
            
            Console.WriteLine("BalatroMobile Build");
            Console.WriteLine("======================");
            Console.WriteLine($"Log file: {logger.LogFilePath}");
            Console.WriteLine();
            
            // Log system information
            logger.LogSystemInfo();
            logger.LogEnvironmentVariables();
            
            Console.WriteLine($"Platform: {config.Platform}");
            Console.WriteLine($"FPS Cap: {config.FpsCap}");
            Console.WriteLine($"Landscape: {config.EnableLandscape}");
            Console.WriteLine($"High DPI: {config.EnableHighDpi}");
            Console.WriteLine($"Disable CRT: {config.DisableCrtShader}");
            Console.WriteLine($"Inject Mods: {config.InjectMods}");
            Console.WriteLine($"Output: {config.OutputPath}");
            Console.WriteLine();
            
            // Log build configuration
            logger.LogBuildConfig(
                config.Platform.ToString(),
                config.FpsCap.ToString(),
                config.EnableLandscape,
                config.EnableHighDpi,
                config.DisableCrtShader,
                config.InjectMods,
                config.OutputPath);

            // Initialize tools manager (handles auto-downloading of required tools)
            Console.WriteLine("Checking required tools...");
            logger.LogInfo("Initializing ToolsManager...");
            var toolsManager = new ToolsManager(msg => {
                Console.WriteLine($"  {msg}");
                logger.LogInfo($"ToolsManager: {msg}");
            });
            
            // Log tool paths
            logger.LogToolPaths(
                toolsManager.GetJavaExecutablePath(),
                toolsManager.ApkToolPath,
                toolsManager.UberApkSignerPath,
                toolsManager.Love2dApkPath,
                toolsManager.ToolsDirectory);
            
            // Log tools directory contents
            logger.LogDirectoryContents(toolsManager.ToolsDirectory, "Tools Directory");
            
            // Check if JDK folder exists and log its contents
            var jdkDir = Path.Combine(toolsManager.ToolsDirectory, "jdk");
            if (Directory.Exists(jdkDir))
            {
                logger.LogDirectoryContents(jdkDir, "JDK Directory");
                var jdkBinDir = Path.Combine(jdkDir, "bin");
                if (Directory.Exists(jdkBinDir))
                {
                    logger.LogDirectoryContents(jdkBinDir, "JDK/bin Directory");
                }
            }
            
            if (!toolsManager.AreToolsAvailable())
            {
                Console.WriteLine();
                Console.WriteLine("Some tools need to be downloaded (first run only)...");
                logger.LogInfo("Some tools need to be downloaded...");
                
                var toolsReady = await toolsManager.EnsureToolsAvailableAsync();
                if (!toolsReady)
                {
                    Console.WriteLine("ERROR: Failed to download required tools.");
                    Console.WriteLine("Please check your internet connection and try again.");
                    logger.LogError("Failed to download required tools");
                    logger.LogFinalResult(false, DateTime.Now - buildStartTime);
                    Console.WriteLine($"\nFull log saved to: {logger.LogFilePath}");
                    return;
                }
                Console.WriteLine();
                
                // Re-log tool paths after download
                logger.LogToolPaths(
                    toolsManager.GetJavaExecutablePath(),
                    toolsManager.ApkToolPath,
                    toolsManager.UberApkSignerPath,
                    toolsManager.Love2dApkPath,
                    toolsManager.ToolsDirectory);
            }

            // Create services using the tools manager paths
            var gameDetector = new GameDetector();
            var patchService = new PatchService();
            var modInjectionService = new ModInjectionService();
            var javaPath = toolsManager.GetJavaExecutablePath();
            var javaTool = new JavaTool(javaPath);
            var apkTool = new ApkTool(javaTool, toolsManager.ApkToolPath, toolsManager.UberApkSignerPath);
            var buildService = new BuildService(gameDetector, patchService, modInjectionService, apkTool, javaTool, toolsManager.Love2dApkPath);

            // Log Balatro detection
            var balatroPath = await gameDetector.GetGameInstallPathAsync();
            logger.LogBalatroDetection(balatroPath, balatroPath != null);

            // Validate environment first with detailed reporting
            Console.WriteLine("Validating build environment...");
            logger.LogSection("BUILD ENVIRONMENT VALIDATION");
            
            // Check each component individually for better error messages
            Console.WriteLine($"  Java path: {javaPath}");
            Console.WriteLine($"  Java exists: {File.Exists(javaPath)}");
            logger.LogInfo($"Java path: {javaPath}");
            logger.LogInfo($"Java file exists: {File.Exists(javaPath)}");
            
            Console.WriteLine($"  APKTool path: {toolsManager.ApkToolPath}");
            Console.WriteLine($"  APKTool exists: {File.Exists(toolsManager.ApkToolPath)}");
            logger.LogInfo($"APKTool path: {toolsManager.ApkToolPath}");
            logger.LogInfo($"APKTool file exists: {File.Exists(toolsManager.ApkToolPath)}");
            
            Console.WriteLine($"  Love2D path: {toolsManager.Love2dApkPath}");
            Console.WriteLine($"  Love2D exists: {File.Exists(toolsManager.Love2dApkPath)}");
            logger.LogInfo($"Love2D path: {toolsManager.Love2dApkPath}");
            logger.LogInfo($"Love2D file exists: {File.Exists(toolsManager.Love2dApkPath)}");
            
            // Test Java can actually run
            Console.WriteLine("  Testing Java...");
            logger.LogInfo("Testing Java execution...");
            
            // Run Java test and capture output for logging
            string? javaStdout = null, javaStderr = null;
            int? javaExitCode = null;
            bool javaTestResult = false;
            
            try
            {
                var javaProcess = new System.Diagnostics.Process();
                javaProcess.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                javaProcess.Start();
                javaStdout = await javaProcess.StandardOutput.ReadToEndAsync();
                javaStderr = await javaProcess.StandardError.ReadToEndAsync();
                await javaProcess.WaitForExitAsync();
                javaExitCode = javaProcess.ExitCode;
                javaTestResult = javaExitCode == 0;
            }
            catch (Exception ex)
            {
                logger.LogException("Java test", ex);
                javaStderr = ex.Message;
            }
            
            Console.WriteLine($"  Java works: {javaTestResult}");
            logger.LogJavaTest(javaPath, javaTestResult, javaExitCode, javaStdout, javaStderr);
            
            // Test APKTool
            Console.WriteLine("  Testing APKTool...");
            logger.LogInfo("Testing APKTool execution...");
            
            string? apkToolOutput = null, apkToolError = null;
            bool apkToolTestResult = false;
            
            try
            {
                var apkToolProcess = new System.Diagnostics.Process();
                apkToolProcess.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = $"-jar \"{toolsManager.ApkToolPath}\" --version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                apkToolProcess.Start();
                apkToolOutput = await apkToolProcess.StandardOutput.ReadToEndAsync();
                apkToolError = await apkToolProcess.StandardError.ReadToEndAsync();
                await apkToolProcess.WaitForExitAsync();
                apkToolTestResult = apkToolProcess.ExitCode == 0 && !string.IsNullOrEmpty(apkToolOutput);
            }
            catch (Exception ex)
            {
                logger.LogException("APKTool test", ex);
                apkToolError = ex.Message;
            }
            
            Console.WriteLine($"  APKTool works: {apkToolTestResult}");
            logger.LogApkToolTest(toolsManager.ApkToolPath, javaPath, apkToolTestResult, apkToolOutput, apkToolError);
            
            // Log validation results
            logger.LogToolValidation("Java", File.Exists(javaPath), javaTestResult, javaStdout ?? javaStderr);
            logger.LogToolValidation("APKTool", File.Exists(toolsManager.ApkToolPath), apkToolTestResult, apkToolOutput ?? apkToolError);
            
            if (!await buildService.ValidateBuildEnvironmentAsync())
            {
                Console.WriteLine();
                Console.WriteLine("ERROR: Build environment validation failed!");
                Console.WriteLine();
                Console.WriteLine("Debug info:");
                Console.WriteLine($"  - Java available: {javaTestResult}");
                Console.WriteLine($"  - APKTool available: {apkToolTestResult}");
                Console.WriteLine($"  - Balatro found: {balatroPath != null}");
                Console.WriteLine();
                
                logger.LogError("Build environment validation failed!");
                logger.LogInfo($"Java available: {javaTestResult}");
                logger.LogInfo($"APKTool available: {apkToolTestResult}");
                logger.LogInfo($"Balatro found: {balatroPath != null}");
                logger.LogFinalResult(false, DateTime.Now - buildStartTime);
                
                Console.WriteLine($"Full diagnostic log saved to:");
                Console.WriteLine($"  {logger.LogFilePath}");
                Console.WriteLine();
                Console.WriteLine("Please share this log file when reporting issues.");
                return;
            }
            Console.WriteLine("OK: Build environment validated");
            logger.LogInfo("Build environment validation PASSED");

            // Progress reporting with logging
            var progress = new Progress<string>(message => {
                Console.WriteLine($"📋 {message}");
                logger.LogBuildStep(message, true);
            });

            Console.WriteLine("Starting build process...");
            logger.LogSection("BUILD PROCESS");
            logger.LogInfo("Starting build...");
            
            var result = await buildService.BuildAsync(config, progress);

            Console.WriteLine();
            Console.WriteLine("Build completed!");
            Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
            Console.WriteLine();

            // Log all messages
            logger.LogSection("BUILD MESSAGES");
            foreach (var message in result.Messages)
            {
                logger.LogInfo($"Build: {message}");
            }

            if (result.Success)
            {
                Console.WriteLine("SUCCESS: Build completed!");
                logger.LogInfo("BUILD SUCCEEDED");
                
                if (!string.IsNullOrEmpty(result.OutputPath))
                {
                    Console.WriteLine($"📱 Output: {result.OutputPath}");
                    logger.LogInfo($"Output file: {result.OutputPath}");
                    
                    if (File.Exists(result.OutputPath))
                    {
                        var outputInfo = new FileInfo(result.OutputPath);
                        logger.LogInfo($"Output size: {outputInfo.Length:N0} bytes ({outputInfo.Length / 1024.0 / 1024.0:F2} MB)");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine("1. Transfer the APK to your Android device");
                Console.WriteLine("2. Install and run the app");
                Console.WriteLine("3. Run 'BalatroMobile transfer' to copy your saves");
                
                logger.LogFinalResult(true, DateTime.Now - buildStartTime);
            }
            else
            {
                Console.WriteLine("ERROR: Build failed!");
                logger.LogError("BUILD FAILED");
                
                Console.WriteLine();
                Console.WriteLine("Errors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                    logger.LogError($"Build error: {error}");
                }
                
                logger.LogFinalResult(false, DateTime.Now - buildStartTime);
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
            
            Console.WriteLine();
            Console.WriteLine($"Full log saved to: {logger.LogFilePath}");
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
        bool hasExistingBuild = File.Exists(existingApk);

        if (hasExistingBuild)
        {
            Console.WriteLine($"Found existing build: {existingApk}");
            bool rebuild = AskYesNo("Would you like to build again?", true);
            if (!rebuild)
            {
                Console.WriteLine("Build cancelled.");
                WaitForKeyPress();
                return;
            }
        }

        // Platform info
        Console.WriteLine("NOTE: ADB is NOT required for building modded APKs - only for save transfer.");
        Console.WriteLine("Building for Android...");
        Console.WriteLine();

        // Check if Balatro is installed - if not, prompt for path
        Console.WriteLine();
        var gameDetector = new GameDetector();
        var balatroPath = await gameDetector.GetGameInstallPathAsync();
        
        if (string.IsNullOrEmpty(balatroPath))
        {
            Console.WriteLine("Balatro installation not found in default locations.");
            var userPath = AskForPath(
                "Please enter the path to your Balatro folder (containing Balatro.exe):",
                @"Example: D:\SteamLibrary\steamapps\common\Balatro",
                path => File.Exists(Path.Combine(path, "Balatro.exe"))
            );
            
            if (string.IsNullOrEmpty(userPath))
            {
                Console.WriteLine("Cannot proceed without Balatro installation.");
                WaitForKeyPress();
                return;
            }
            
            // Set the override path for the game detector
            GameDetector.OverrideGamePath = userPath;
            Console.WriteLine($"Using Balatro path: {userPath}");
        }
        else
        {
            Console.WriteLine($"Found Balatro at: {balatroPath}");
        }

        // Run pre-flight checks
        Console.WriteLine();
        await RunPreFlightChecks();
        Console.WriteLine();

        // Configure build options interactively (like original patching questions)
        var buildArgs = new List<string>();

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
        Console.Write("Output filename (press Enter for default 'balatro.apk'): ");
        string outputFile = Console.ReadLine()?.Trim() ?? "";
        if (!string.IsNullOrEmpty(outputFile))
        {
            // Validate the output path
            var validatedPath = ValidateOutputPath(outputFile);
            if (validatedPath == null)
            {
                Console.WriteLine("Using default output path instead.");
                outputFile = "";
            }
            else
            {
                outputFile = validatedPath;
                buildArgs.Add("--output");
                buildArgs.Add(outputFile);
            }
        }
        
        // Also validate default path if user didn't specify custom
        if (string.IsNullOrEmpty(outputFile))
        {
            if (File.Exists("balatro.apk"))
            {
                bool overwrite = AskYesNo("Default output 'balatro.apk' already exists. Overwrite?", true);
                if (!overwrite)
                {
                    Console.WriteLine("Build cancelled - please specify a different output filename.");
                    WaitForKeyPress();
                    return;
                }
            }
        }

        // Show configuration summary
        Console.WriteLine();
        Console.WriteLine("Build configuration:");
        Console.WriteLine("Platform: Android");
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
        if (File.Exists("balatro.apk"))
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

    private static async Task RunToolsCommand(string[] args)
    {
        var toolsManager = new ToolsManager(msg => Console.WriteLine($"  {msg}"));

        if (args.Length == 0 || args[0] == "status")
        {
            // Show tools status
            Console.WriteLine("BalatroMobile Tools Status");
            Console.WriteLine("==========================");
            Console.WriteLine();
            Console.WriteLine($"Tools directory: {toolsManager.ToolsDirectory}");
            Console.WriteLine($"Cache size: {toolsManager.GetCacheSize() / 1024 / 1024:F1} MB");
            Console.WriteLine();

            Console.WriteLine("Tool availability:");
            Console.WriteLine($"  Java:            {(toolsManager.IsJavaAvailable() ? "Available" : "Not found")}");
            Console.WriteLine($"  APKTool:         {(File.Exists(toolsManager.ApkToolPath) ? "Available" : "Not found")}");
            Console.WriteLine($"  uber-apk-signer: {(File.Exists(toolsManager.UberApkSignerPath) ? "Available" : "Not found")}");
            Console.WriteLine($"  Love2D APK:      {(File.Exists(toolsManager.Love2dApkPath) ? "Available" : "Not found")}");
            Console.WriteLine();

            if (!toolsManager.AreToolsAvailable())
            {
                Console.WriteLine("Some tools are missing. Run 'BalatroMobile tools download' to download them.");
            }
            else
            {
                Console.WriteLine("All tools are available.");
            }
        }
        else if (args[0] == "download")
        {
            Console.WriteLine("Downloading required tools...");
            Console.WriteLine();
            var success = await toolsManager.EnsureToolsAvailableAsync();
            Console.WriteLine();
            if (success)
            {
                Console.WriteLine("All tools downloaded successfully.");
            }
            else
            {
                Console.WriteLine("ERROR: Some tools failed to download. Check your internet connection.");
            }
        }
        else if (args[0] == "clear")
        {
            Console.Write("This will delete all cached tools. Are you sure? (y/N): ");
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response == "y" || response == "yes")
            {
                toolsManager.ClearCache();
                Console.WriteLine("Tool cache cleared.");
            }
            else
            {
                Console.WriteLine("Cancelled.");
            }
        }
        else
        {
            Console.WriteLine("Unknown tools command.");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  tools status   - Show tools status (default)");
            Console.WriteLine("  tools download - Download all required tools");
            Console.WriteLine("  tools clear    - Clear cached tools");
        }
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
        Console.WriteLine("  BalatroMobile tools [status|download|clear] - Manage build tools");
        Console.WriteLine();
        Console.WriteLine("Build Options:");
        Console.WriteLine("  --fps <default|none|60>             - FPS cap (default: default)");
        Console.WriteLine("  --no-landscape                       - Disable landscape lock");
        Console.WriteLine("  --high-dpi                           - Enable high DPI mode");
        Console.WriteLine("  --disable-crt                        - Disable CRT shader");
        Console.WriteLine("  --inject-mods                        - Inject mods during build (ADB not required)");
        Console.WriteLine("  --output <path>                      - Output file path");
        Console.WriteLine();
        Console.WriteLine("Transfer Options (requires ADB):");
        Console.WriteLine("  --from <pc|android>                  - Source platform (default: pc)");
        Console.WriteLine("  --to <android|pc>                    - Target platform (default: android)");
        Console.WriteLine("  --no-backup                          - Skip backup creation");
        Console.WriteLine("  --no-mods                            - Skip mod-related files");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  BalatroMobile build --inject-mods    # Create modded APK (no ADB needed)");
        Console.WriteLine("  BalatroMobile transfer               # Transfer saves (requires ADB)");
        Console.WriteLine("  BalatroMobile transfer --from android --to pc");
        Console.WriteLine();
        Console.WriteLine("NOTE: ADB is only required for save transfer, not for building modded APKs.");
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


    /// <summary>
    /// Prompts user to enter a path when auto-detection fails.
    /// Returns null if user skips (empty input).
    /// </summary>
    private static string? AskForPath(string prompt, string hint, Func<string, bool> validator)
    {
        Console.WriteLine();
        Console.WriteLine(prompt);
        if (!string.IsNullOrEmpty(hint))
        {
            Console.WriteLine($"   Hint: {hint}");
        }
        Console.WriteLine("   (Press Enter to skip)");

        while (true)
        {
            Console.Write("> ");
            var path = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(path))
            {
                return null; // User skipped
            }

            // Remove quotes if user pasted a quoted path
            path = path.Trim('"');

            if (validator(path))
            {
                return path;
            }

            Console.WriteLine("   Invalid path. Please try again or press Enter to skip.");
        }
    }

    /// <summary>
    /// Validates and potentially prompts for confirmation on output path.
    /// Returns the validated path, or null if user cancels.
    /// </summary>
    private static string? ValidateOutputPath(string outputPath)
    {
        // Check if parent directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Console.WriteLine($"Directory '{directory}' does not exist.");
            bool create = AskYesNo("Would you like to create it?", true);
            if (create)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"Created directory: {directory}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create directory: {ex.Message}");
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        // Check if file already exists
        if (File.Exists(outputPath))
        {
            bool overwrite = AskYesNo($"File '{outputPath}' already exists. Overwrite?", false);
            if (!overwrite)
            {
                return null;
            }
        }

        // Validate filename characters
        var fileName = Path.GetFileName(outputPath);
        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalidChars) >= 0)
        {
            Console.WriteLine($"Invalid characters in filename: {fileName}");
            return null;
        }

        return outputPath;
    }
}