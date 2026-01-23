using BalatroMobile.Configuration.Models;
using BalatroMobile.Configuration.Services;
using BalatroMobile.Core.Services.GameDetection;

namespace BalatroMobile.Core.Services;

public class PreFlightCheckService : IPreFlightCheckService
{
    private readonly IGameDetector _gameDetector;
    private readonly IPlatformDetector _platformDetector;

    public PreFlightCheckService(
        IGameDetector gameDetector,
        IPlatformDetector platformDetector)
    {
        _gameDetector = gameDetector;
        _platformDetector = platformDetector;
    }

    public async Task<IEnumerable<PreFlightCheck>> GetAllChecksAsync()
    {
        return new List<PreFlightCheck>
        {
            // Game Installation Checks
            new PreFlightCheck
            {
                Name = "SteamBalatroInstalled",
                Description = "Steam version of Balatro is installed",
                CheckFunction = CheckSteamBalatroInstalled,
                IsRequired = true,
                FixSuggestion = "Install Balatro from Steam (https://store.steampowered.com/app/2379780/Balatro/)"
            },

            // Mod Setup Checks
            new PreFlightCheck
            {
                Name = "ModsFolderStructure",
                Description = "Mods folder has correct structure (Lovely, Steamodded, Cryptid, etc.)",
                CheckFunction = CheckModsFolderStructure,
                IsRequired = true,
                FixSuggestion = "Ensure all mods are properly installed in %APPDATA%\\Balatro\\Mods\\"
            },

            new PreFlightCheck
            {
                Name = "LovelyInjectorWorking",
                Description = "Lovely injector is installed and working",
                CheckFunction = CheckLovelyInjector,
                IsRequired = true,
                FixSuggestion = "Reinstall Lovely injector (version.dll should be in Balatro.exe folder)"
            },

            new PreFlightCheck
            {
                Name = "LovelyDumpExists",
                Description = "Lovely dump exists and is not empty",
                CheckFunction = CheckLovelyDump,
                IsRequired = true,
                FixSuggestion = "Launch modded Balatro on PC, wait 10 seconds on main menu, then exit"
            },

            new PreFlightCheck
            {
                Name = "ModsWorkingOnPC",
                Description = "Mods work correctly on PC (Cryptid content visible)",
                CheckFunction = CheckModsWorkingOnPC,
                IsRequired = true,
                FixSuggestion = "Start a new run and verify Cryptid content appears (new decks, jokers, etc.)"
            },

            // Android Setup Checks
            new PreFlightCheck
            {
                Name = "AndroidDeveloperOptions",
                Description = "Android developer options are enabled",
                CheckFunction = CheckAndroidDeveloperOptions,
                IsRequired = true,
                FixSuggestion = "Settings → About phone → Tap Build number 7 times to enable developer options"
            },

            new PreFlightCheck
            {
                Name = "USBDebuggingEnabled",
                Description = "USB debugging is enabled on Android device (only needed for save transfer)",
                CheckFunction = CheckUSBDebugging,
                IsRequired = false, // Not required for building modded APKs
                FixSuggestion = "Settings → Additional settings → Developer options → Enable USB debugging (optional)"
            },

            new PreFlightCheck
            {
                Name = "ADBConnectionWorking",
                Description = "ADB can communicate with Android device",
                CheckFunction = CheckADBConnection,
                IsRequired = true,
                FixSuggestion = "Connect Android device via USB, accept RSA prompt, run 'adb devices' to verify"
            },

            new PreFlightCheck
            {
                Name = "AndroidStorageSpace",
                Description = "Android device has sufficient storage space",
                CheckFunction = CheckAndroidStorage,
                IsRequired = false,
                FixSuggestion = "Free up at least 500MB on Android device"
            },

            // Development Environment Checks
            new PreFlightCheck
            {
                Name = "JavaRuntimeAvailable",
                Description = "Java runtime is available for APK building",
                CheckFunction = CheckJavaRuntime,
                IsRequired = false, // Will be auto-downloaded if missing
                FixSuggestion = "Java will be auto-downloaded on first build, or install OpenJDK manually"
            },

            new PreFlightCheck
            {
                Name = "InternetConnection",
                Description = "Internet connection available for downloading tools",
                CheckFunction = CheckInternetConnection,
                IsRequired = true,
                FixSuggestion = "Ensure stable internet connection for downloading build tools"
            }
        };
    }

    public async Task<PreFlightCheckResult> RunAllChecksAsync()
    {
        var checks = await GetAllChecksAsync();
        var results = new List<CheckResultDetail>();
        var errors = new List<string>();

        foreach (var check in checks)
        {
            try
            {
                var result = await RunCheckAsync(check.Name);
                results.AddRange(result.Results);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to run check '{check.Name}': {ex.Message}");
                results.Add(new CheckResultDetail
                {
                    CheckName = check.Name,
                    Result = CheckResult.Unknown,
                    Message = $"Check execution failed: {ex.Message}"
                });
            }
        }

        return new PreFlightCheckResult
        {
            AllPassed = results.All(r => r.Result == CheckResult.Pass),
            Results = results,
            Errors = errors
        };
    }

    public async Task<PreFlightCheckResult> RunCheckAsync(string checkName)
    {
        var checks = await GetAllChecksAsync();
        var check = checks.FirstOrDefault(c => c.Name == checkName);

        if (check == null)
        {
            return new PreFlightCheckResult
            {
                AllPassed = false,
                Results = new[] { new CheckResultDetail
                {
                    CheckName = checkName,
                    Result = CheckResult.Unknown,
                    Message = $"Check '{checkName}' not found"
                }},
                Errors = new[] { $"Check '{checkName}' not found" }
            };
        }

        try
        {
            var result = await check.CheckFunction();
            return new PreFlightCheckResult
            {
                AllPassed = result == CheckResult.Pass,
                Results = new[] { new CheckResultDetail
                {
                    CheckName = check.Name,
                    Result = result,
                    Message = GetResultMessage(check, result),
                    FixSuggestion = check.FixSuggestion
                }}
            };
        }
        catch (Exception ex)
        {
            return new PreFlightCheckResult
            {
                AllPassed = false,
                Results = new[] { new CheckResultDetail
                {
                    CheckName = check.Name,
                    Result = CheckResult.Fail,
                    Message = $"Check failed: {ex.Message}",
                    FixSuggestion = check.FixSuggestion
                }},
                Errors = new[] { ex.Message }
            };
        }
    }

    // Individual check implementations
    private async Task<CheckResult> CheckSteamBalatroInstalled()
    {
        var installed = await _gameDetector.IsSteamBalatroInstalledAsync();
        return installed ? CheckResult.Pass : CheckResult.Fail;
    }

    private async Task<CheckResult> CheckModsFolderStructure()
    {
        var modValidator = new ModValidator();
        var valid = await modValidator.ValidateModsFolderStructureAsync();
        return valid ? CheckResult.Pass : CheckResult.Fail;
    }

    private async Task<CheckResult> CheckLovelyInjector()
    {
        var modValidator = new ModValidator();
        var working = await modValidator.IsLovelyInjectorWorkingAsync();
        return working ? CheckResult.Pass : CheckResult.Fail;
    }

    private async Task<CheckResult> CheckLovelyDump()
    {
        var modValidator = new ModValidator();
        var exists = await modValidator.DoesLovelyDumpExistAsync();
        return exists ? CheckResult.Pass : CheckResult.Fail;
    }

    private async Task<CheckResult> CheckModsWorkingOnPC()
    {
        var modValidator = new ModValidator();
        var working = await modValidator.AreModsWorkingOnPCAsync();
        return working ? CheckResult.Pass : CheckResult.Warning; // Warning because we can't fully verify without running the game
    }

    private async Task<CheckResult> CheckAndroidDeveloperOptions()
    {
        var enabled = await _platformDetector.AreAndroidDeveloperOptionsEnabledAsync();
        return enabled ? CheckResult.Pass : CheckResult.Warning; // Warning because we may not be able to check
    }

    private async Task<CheckResult> CheckUSBDebugging()
    {
        var enabled = await _platformDetector.IsUSBDebuggingEnabledAsync();
        return enabled ? CheckResult.Pass : CheckResult.Warning; // Warning since only needed for save transfer
    }

    private async Task<CheckResult> CheckADBConnection()
    {
        var working = await _platformDetector.IsADBConnectionWorkingAsync();
        return working ? CheckResult.Pass : CheckResult.Fail;
    }

    private async Task<CheckResult> CheckAndroidStorage()
    {
        var sufficient = await _platformDetector.HasAndroidSufficientStorageAsync();
        return sufficient ? CheckResult.Pass : CheckResult.Warning;
    }

    private async Task<CheckResult> CheckJavaRuntime()
    {
        var available = await _platformDetector.IsJavaRuntimeAvailableAsync();
        return available ? CheckResult.Pass : CheckResult.Fail;
    }

    private async Task<CheckResult> CheckInternetConnection()
    {
        var available = await _platformDetector.IsInternetConnectionAvailableAsync();
        return available ? CheckResult.Pass : CheckResult.Warning; // Warning because internet is needed for tool downloads
    }

    private static string GetResultMessage(PreFlightCheck check, CheckResult result)
    {
        return result switch
        {
            CheckResult.Pass => $"{check.Description}: ✓ PASSED",
            CheckResult.Fail => $"{check.Description}: ✗ FAILED",
            CheckResult.Warning => $"{check.Description}: ⚠ WARNING",
            CheckResult.Unknown => $"{check.Description}: ? UNKNOWN",
            _ => $"{check.Description}: ? UNKNOWN"
        };
    }
}