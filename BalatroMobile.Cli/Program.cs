using BalatroMobile.Configuration.Services;
using BalatroMobile.Core.Services;
using BalatroMobile.Core.Services.GameDetection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

namespace BalatroMobile.Cli;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("BalatroMobile - Build Balatro for Mobile Devices");

        var checkCommand = new Command("check", "Run pre-flight checks to verify system readiness");
        var buildCommand = new Command("build", "Build Balatro for mobile devices");
        var transferCommand = new Command("transfer", "Transfer saves between PC and mobile");

        rootCommand.AddCommand(checkCommand);
        rootCommand.AddCommand(buildCommand);
        rootCommand.AddCommand(transferCommand);

        checkCommand.SetHandler(async () =>
        {
            await RunPreFlightChecks();
        });

        await rootCommand.InvokeAsync(args);
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
            new ModValidator(),
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

internal class ModValidator : IModValidator
{
    public Task<bool> ValidateModsFolderStructureAsync() => Task.FromResult(true);
    public Task<bool> IsLovelyInjectorWorkingAsync() => Task.FromResult(true);
    public Task<bool> DoesLovelyDumpExistAsync() => Task.FromResult(true);
    public Task<bool> AreModsWorkingOnPCAsync() => Task.FromResult(true);
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