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
            else if (args[0] == "tools")
            {
                await RunToolsCommand(args.Skip(1).ToArray());
            }
            else if (args[0] == "mods")
            {
                await RunModsCommand(args.Skip(1).ToArray());
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
            Console.WriteLine($"Output: {config.OutputPath}");
            Console.WriteLine();
            
            // Log build configuration
            logger.LogBuildConfig(
                config.Platform.ToString(),
                config.FpsCap.ToString(),
                config.EnableLandscape,
                config.EnableHighDpi,
                config.DisableCrtShader,
                false, // Mods are not injected into APK
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
            var javaPath = toolsManager.GetJavaExecutablePath();
            var javaTool = new JavaTool(javaPath);
            var apkTool = new ApkTool(javaTool, toolsManager.ApkToolPath, toolsManager.UberApkSignerPath);
            var buildService = new BuildService(
                gameDetector, 
                patchService, 
                apkTool, 
                javaTool, 
                toolsManager.Love2dApkPath,
                toolsManager.BalatroApkPatchPath);

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
                Console.WriteLine("2. Install and launch the app once");
                Console.WriteLine("3. Transfer mods to device (via USB/ADB or manually)");
                
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

    private static async Task RunInteractiveMode()
    {
        Console.WriteLine("=====================================");
        Console.WriteLine("    BalatroMobile Interactive Mode");
        Console.WriteLine("=====================================");
        Console.WriteLine();

        // Check if Balatro.exe is in the current directory
        var currentDir = Environment.CurrentDirectory;
        var balatroExePath = Path.Combine(currentDir, "Balatro.exe");
        var gameLovePath = Path.Combine(currentDir, "Game.love");
        
        if (!File.Exists(balatroExePath) && !File.Exists(gameLovePath))
        {
            Console.WriteLine("┌─────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│  Balatro.exe not found in current folder!                       │");
            Console.WriteLine("│                                                                 │");
            Console.WriteLine("│  Please copy Balatro.exe here:                                  │");
            Console.WriteLine("│                                                                 │");
            Console.WriteLine("│  1. Open Steam                                                  │");
            Console.WriteLine("│  2. Right-click 'Balatro' in your library                       │");
            Console.WriteLine("│  3. Click 'Manage' -> 'Browse local files'                      │");
            Console.WriteLine("│  4. Copy 'Balatro.exe' to this folder:                          │");
            Console.WriteLine($"│     {currentDir,-55} │");
            Console.WriteLine("│  5. Run BalatroMobile.exe again                                 │");
            Console.WriteLine("│                                                                 │");
            Console.WriteLine("│  NOTE: Your original game files are NOT modified.               │");
            Console.WriteLine("│        We work with the copy you provide.                       │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            WaitForKeyPress();
            return;
        }

        var gameFile = File.Exists(balatroExePath) ? "Balatro.exe" : "Game.love";
        Console.WriteLine($"Found {gameFile} in current folder - ready to build!");
        Console.WriteLine();
        
        // Check for mods
        var gameDetector = new GameDetector();
        if (gameDetector.HasModsInstalled())
        {
            Console.WriteLine($"Mods folder: {gameDetector.GetModsFolderPath()}");
            if (gameDetector.HasLovelyDump())
            {
                var dumpFiles = Directory.GetFiles(gameDetector.GetLovelyDumpPath(), "*.lua", SearchOption.AllDirectories);
                Console.WriteLine($"Lovely dump: {dumpFiles.Length} Lua files found");
            }
            Console.WriteLine();
        }

        // Show default configuration and ask if user wants quick build or customize
        Console.WriteLine("=== DEFAULT BUILD SETTINGS ===");
        Console.WriteLine();
        Console.WriteLine("  Platform:        Android");
        Console.WriteLine("  FPS Cap:         60 FPS");
        Console.WriteLine("  Landscape Lock:  Yes");
        Console.WriteLine("  High DPI:        Yes");
        Console.WriteLine("  CRT Shader:      Enabled (disable for Pixel devices)");
        Console.WriteLine("  Output:          balatro.apk");
        Console.WriteLine();
        Console.WriteLine("==============================");
        Console.WriteLine();

        // Ask for quick build or customization
        Console.WriteLine("Options:");
        Console.WriteLine("  [1] Quick Build - Use defaults above and start immediately");
        Console.WriteLine("  [2] Customize   - Configure each option manually");
        Console.WriteLine("  [3] Exit        - Cancel and exit");
        Console.WriteLine();
        
        int choice = AskChoice("Select option", 1, 3);
        
        if (choice == 3)
        {
            Console.WriteLine("Build cancelled.");
            WaitForKeyPress();
            return;
        }

        // Default values
        bool cleanupAfter = true;
        bool verboseMode = false;
        bool applyFpsPatch = true;
        int fpsChoice = 2; // 60 FPS
        bool applyLandscapePatch = true;
        bool applyHighDpiPatch = true;
        bool applyCrtPatch = false;
        string outputFile = "balatro.apk";

        if (choice == 2)
        {
            // Customization mode - ask all questions
            Console.WriteLine();
            Console.WriteLine("=== CUSTOMIZATION MODE ===");
            Console.WriteLine();

            cleanupAfter = AskYesNo("Automatically clean up temp files?", true);
            verboseMode = AskYesNo("Enable extra logging?", false);

            // FPS patch
            applyFpsPatch = AskYesNo("Apply FPS cap patch?", true);
            if (applyFpsPatch)
            {
                Console.WriteLine();
                Console.WriteLine("FPS options:");
                Console.WriteLine("  1. Device default (matches screen refresh rate)");
                Console.WriteLine("  2. 60 FPS (recommended)");
                Console.WriteLine("  3. No limit");
                fpsChoice = AskChoice("Choose FPS setting", 1, 3);
            }

            applyLandscapePatch = AskYesNo("Apply landscape orientation lock?", true);
            applyHighDpiPatch = AskYesNo("Apply high DPI patch? (recommended for high-res screens)", true);
            applyCrtPatch = AskYesNo("Disable CRT shader? (Required for Pixel and some devices!)", false);

            Console.WriteLine();
            Console.Write("Output filename (Enter for 'balatro.apk'): ");
            var userOutput = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(userOutput))
            {
                var validatedPath = ValidateOutputPath(userOutput);
                if (validatedPath != null)
                {
                    outputFile = validatedPath;
                }
            }
        }
        else
        {
            // Quick build mode
            Console.WriteLine();
            Console.WriteLine("Starting quick build with default settings...");
        }

        // Build the args
        var buildArgs = new List<string>();

        if (applyFpsPatch)
        {
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

        if (applyLandscapePatch)
        {
            buildArgs.Add("--no-landscape");
        }

        if (applyHighDpiPatch)
        {
            buildArgs.Add("--high-dpi");
        }

        if (applyCrtPatch)
        {
            buildArgs.Add("--disable-crt");
        }

        if (outputFile != "balatro.apk")
        {
            buildArgs.Add("--output");
            buildArgs.Add(outputFile);
        }

        // Check for existing output file
        if (File.Exists(outputFile))
        {
            if (choice == 1)
            {
                // Quick mode - just warn and continue
                Console.WriteLine($"Note: Existing '{outputFile}' will be overwritten.");
            }
            else
            {
                bool overwrite = AskYesNo($"'{outputFile}' already exists. Overwrite?", true);
                if (!overwrite)
                {
                    Console.WriteLine("Build cancelled - please specify a different output filename.");
                    WaitForKeyPress();
                    return;
                }
            }
        }

        // Show configuration summary (for custom mode) or just start (for quick mode)
        if (choice == 2)
        {
            Console.WriteLine();
            Console.WriteLine("=== FINAL BUILD CONFIGURATION ===");
            Console.WriteLine($"  Platform:        Android");
            Console.WriteLine($"  FPS Cap:         {(applyFpsPatch ? (fpsChoice == 1 ? "Device Default" : fpsChoice == 2 ? "60 FPS" : "No Limit") : "No")}");
            Console.WriteLine($"  Landscape Lock:  {(applyLandscapePatch ? "Yes" : "No")}");
            Console.WriteLine($"  High DPI:        {(applyHighDpiPatch ? "Yes" : "No")}");
            Console.WriteLine($"  CRT Shader:      {(applyCrtPatch ? "Disabled" : "Enabled")}");
            Console.WriteLine($"  Output:          {outputFile}");
            Console.WriteLine("=================================");
            Console.WriteLine();

            bool confirm = AskYesNo("Start build with these settings?", true);
            if (!confirm)
            {
                Console.WriteLine("Build cancelled.");
                WaitForKeyPress();
                return;
            }
        }

        // Run the build
        Console.WriteLine();
        Console.WriteLine("Starting build process...");
        Console.WriteLine();
        await RunBuild(buildArgs.ToArray());

        // Post-build options
        if (File.Exists(outputFile))
        {
            Console.WriteLine();
            Console.WriteLine("Build completed successfully!");
            Console.WriteLine();
            
            // Ask about mod transfer (the main use case)
            bool transferMods = AskYesNo("Transfer mods to Android device?", true);
            if (transferMods)
            {
                Console.WriteLine();
                Console.WriteLine("=== MOD TRANSFER ===");
                Console.WriteLine();
                
                // Run ADB diagnostics first
                Console.WriteLine("Checking ADB connection...");
                var (adbOk, adbIssues) = await RunAdbDiagnosticsAsync();
                
                if (!adbOk)
                {
                    Console.WriteLine();
                    Console.WriteLine("ADB connection issues detected:");
                    foreach (var issue in adbIssues)
                    {
                        Console.WriteLine($"  {issue}");
                    }
                    Console.WriteLine();
                    
                    bool continueAnyway = AskYesNo("Try ADB transfer anyway?", false);
                    if (!continueAnyway)
                    {
                        // Prepare mods package but show manual instructions
                        Console.WriteLine();
                        Console.WriteLine("Preparing mod package for manual transfer...");
                        var modsArgsManual = new List<string> { "--prepare-only" };
                        await RunModsCommand(modsArgsManual.ToArray());
                        Console.WriteLine();
                        WaitForKeyPress();
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("ADB connection OK!");
                    Console.WriteLine();
                }
                
                bool installFirst = AskYesNo("Install APK to device first?", true);
                
                var modsArgs = new List<string>();
                if (installFirst)
                {
                    modsArgs.Add("--install-apk");
                    modsArgs.Add("--apk");
                    modsArgs.Add(outputFile);
                }
                
                await RunModsCommand(modsArgs.ToArray());
            }
            else
            {
                // User chose not to transfer - ask if they want the mod package prepared
                bool prepareMods = AskYesNo("Prepare mod package for manual transfer?", true);
                if (prepareMods)
                {
                    await RunModsCommand(new[] { "--prepare-only" });
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

    private static async Task RunModsCommand(string[] args)
    {
        Console.WriteLine("BalatroMobile Mod Transfer");
        Console.WriteLine("==========================");
        Console.WriteLine();

        var modService = new ModTransferService(msg => Console.WriteLine($"  {msg}"));

        // Parse arguments
        bool prepareOnly = args.Contains("--prepare-only");
        bool transferOnly = args.Contains("--transfer-only");
        bool installApk = args.Contains("--install-apk");
        string? apkPath = null;
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--apk" && i + 1 < args.Length)
            {
                apkPath = args[i + 1];
            }
        }

        var outputDir = Path.Combine(Environment.CurrentDirectory, "mod-package");

        if (!transferOnly)
        {
            // Step 1: Prepare mod package
            Console.WriteLine("Step 1: Preparing mod package...");
            Console.WriteLine();
            
            var prepareResult = await modService.PrepareModPackageAsync(outputDir);
            
            if (!prepareResult.Success)
            {
                Console.WriteLine();
                Console.WriteLine("ERROR: Failed to prepare mod package!");
                foreach (var error in prepareResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                return;
            }
            
            Console.WriteLine();
            Console.WriteLine("Mod package prepared successfully!");
            Console.WriteLine($"Output: {prepareResult.OutputPath}");
            Console.WriteLine();

            if (prepareOnly)
            {
                ShowManualTransferInstructions(prepareResult.OutputPath ?? outputDir);
                return;
            }
        }

        // Step 2: Install APK if specified
        if (installApk || !string.IsNullOrEmpty(apkPath))
        {
            apkPath ??= Path.Combine(Environment.CurrentDirectory, "balatro.apk");
            
            if (!File.Exists(apkPath))
            {
                Console.WriteLine($"APK not found: {apkPath}");
                Console.WriteLine("Please build the APK first.");
                return;
            }
            
            Console.WriteLine("Step 2: Installing APK...");
            Console.WriteLine();
            
            var installed = await modService.InstallApkAsync(apkPath);
            if (!installed)
            {
                Console.WriteLine("WARNING: APK installation failed. Please install manually.");
            }
            else
            {
                // Launch app once to create directories
                Console.WriteLine();
                Console.WriteLine("Launching app to create directories...");
                await modService.LaunchAppAsync();
                await Task.Delay(3000); // Wait 3 seconds
                await modService.StopAppAsync();
                Console.WriteLine();
            }
        }

        // Step 3: Transfer mods to device
        Console.WriteLine("Step 3: Transferring mods to device...");
        Console.WriteLine();
        
        var gamePath = Path.Combine(outputDir, "game");
        if (!Directory.Exists(gamePath))
        {
            Console.WriteLine("ERROR: Mod package not found. Please prepare the mod package first.");
            return;
        }
        
        var transferResult = await modService.TransferToDeviceAsync(gamePath);
        
        if (!transferResult.Success)
        {
            Console.WriteLine();
            Console.WriteLine("ERROR: ADB transfer failed!");
            foreach (var error in transferResult.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
            Console.WriteLine();
            
            // Show manual transfer instructions as fallback
            ShowManualTransferInstructions(gamePath);
            return;
        }
        
        Console.WriteLine();
        Console.WriteLine("=== SUCCESS ===");
        Console.WriteLine();
        Console.WriteLine("Mods have been transferred to your device!");
        Console.WriteLine("Launch Balatro on your device to play with mods.");
        Console.WriteLine();
    }

    private static void ShowUsage()
    {
        Console.WriteLine("BalatroMobile - Build Balatro for Mobile Devices");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  BalatroMobile                        - Interactive mode (RECOMMENDED)");
        Console.WriteLine("  BalatroMobile check                  - Run pre-flight checks");
        Console.WriteLine("  BalatroMobile build [options]        - Build APK for mobile");
        Console.WriteLine("  BalatroMobile mods [options]         - Prepare and transfer mods to device");
        Console.WriteLine("  BalatroMobile tools [status|download|clear] - Manage build tools");
        Console.WriteLine();
        Console.WriteLine("RECOMMENDED: Just run 'BalatroMobile' for interactive mode!");
        Console.WriteLine("            Everything is handled automatically with helpful prompts.");
        Console.WriteLine();
        Console.WriteLine("Build Options:");
        Console.WriteLine("  --fps <default|none|60>             - FPS cap (default: default)");
        Console.WriteLine("  --no-landscape                       - Disable landscape lock");
        Console.WriteLine("  --high-dpi                           - Enable high DPI mode");
        Console.WriteLine("  --disable-crt                        - Disable CRT shader");
        Console.WriteLine("  --output <path>                      - Output file path");
        Console.WriteLine();
        Console.WriteLine("Mods Options:");
        Console.WriteLine("  --prepare-only                       - Only prepare mod package (for manual transfer)");
        Console.WriteLine("  --transfer-only                      - Only transfer (use existing package)");
        Console.WriteLine("  --install-apk                        - Install APK before transferring mods");
        Console.WriteLine("  --apk <path>                         - Path to APK file (default: balatro.apk)");
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
    /// Shows detailed manual transfer instructions when ADB fails
    /// </summary>
    private static void ShowManualTransferInstructions(string modPackagePath)
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│               MANUAL MOD TRANSFER INSTRUCTIONS                  │");
        Console.WriteLine("├─────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("│  ADB transfer couldn't complete. You can transfer manually:    │");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("│  STEP 1: Locate the mod package folder on your PC:             │");
        Console.WriteLine($"│    {TruncatePath(modPackagePath, 55),-55} │");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("│  STEP 2: On your Android device, navigate to:                  │");
        Console.WriteLine("│    /sdcard/Android/data/com.unofficial.balatro/files/save/     │");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("│    OR use a file manager app (like 'Files by Google')          │");
        Console.WriteLine("│    to access Android/data/com.unofficial.balatro/files/save/   │");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("│  STEP 3: Copy the CONTENTS of the 'game' folder to 'save':     │");
        Console.WriteLine("│    - Copy: Mods/, SMODS/, nativefs/, lovely/, lovely.lua       │");
        Console.WriteLine("│    - And all .lua files (main.lua, game.lua, etc.)             │");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("│  STEP 4: Launch Balatro on your device                         │");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("│  NOTE: On Android 11+, you may need to use a PC file manager   │");
        Console.WriteLine("│        or ADB to access the app data folder.                   │");
        Console.WriteLine("│                                                                 │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"Mod package location: {modPackagePath}");
    }

    /// <summary>
    /// Truncates a path for display in fixed-width boxes
    /// </summary>
    private static string TruncatePath(string path, int maxLength)
    {
        if (path.Length <= maxLength) return path;
        return "..." + path.Substring(path.Length - maxLength + 3);
    }

    /// <summary>
    /// Runs ADB diagnostic checks and returns detailed status
    /// </summary>
    private static async Task<(bool Success, string[] Issues)> RunAdbDiagnosticsAsync()
    {
        var issues = new List<string>();
        
        // Check 1: Is ADB installed?
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                issues.Add("ADB not found - please install Android SDK Platform Tools");
                return (false, issues.ToArray());
            }

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                issues.Add("ADB installation appears corrupted");
            }
        }
        catch (Exception)
        {
            issues.Add("ADB not found in PATH - install Android SDK Platform Tools and add to PATH");
            return (false, issues.ToArray());
        }

        // Check 2: Is any device connected?
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "devices",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return (false, new[] { "Failed to run adb devices" });

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var deviceLines = lines.Where(l => !l.StartsWith("List of") && l.Contains("\t")).ToList();

            if (deviceLines.Count == 0)
            {
                issues.Add("No Android device detected");
                issues.Add("  - Connect your device via USB");
                issues.Add("  - Enable USB debugging in Developer Options");
                issues.Add("  - Accept the USB debugging prompt on your device");
            }
            else
            {
                foreach (var line in deviceLines)
                {
                    if (line.Contains("unauthorized"))
                    {
                        issues.Add($"Device unauthorized: {line.Split('\t')[0]}");
                        issues.Add("  - Check your device for a USB debugging authorization prompt");
                        issues.Add("  - Tap 'Allow' on your device");
                    }
                    else if (line.Contains("offline"))
                    {
                        issues.Add($"Device offline: {line.Split('\t')[0]}");
                        issues.Add("  - Disconnect and reconnect the USB cable");
                        issues.Add("  - Try a different USB port");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add($"Error checking devices: {ex.Message}");
        }

        // Check 3: Is the app installed?
        if (issues.Count == 0)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "shell pm list packages com.unofficial.balatro",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (!output.Contains("com.unofficial.balatro"))
                    {
                        issues.Add("Balatro app not installed on device");
                        issues.Add("  - Install the APK first, then try transferring mods");
                    }
                }
            }
            catch { /* Ignore */ }
        }

        return (issues.Count == 0, issues.ToArray());
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