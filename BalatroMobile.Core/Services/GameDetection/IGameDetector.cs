namespace BalatroMobile.Core.Services.GameDetection;

public interface IGameDetector
{
    Task<bool> IsSteamBalatroInstalledAsync();
    Task<string?> GetGameInstallPathAsync();
    Task<bool> IsGameWorkingAsync();
}

public interface IModValidator
{
    Task<bool> ValidateModsFolderStructureAsync();
    Task<bool> IsLovelyInjectorWorkingAsync();
    Task<bool> DoesLovelyDumpExistAsync();
    Task<bool> AreModsWorkingOnPCAsync();
}

public interface IPlatformDetector
{
    Task<bool> AreAndroidDeveloperOptionsEnabledAsync();
    Task<bool> IsUSBDebuggingEnabledAsync();
    Task<bool> IsADBConnectionWorkingAsync();
    Task<bool> HasAndroidSufficientStorageAsync();
    Task<bool> IsJavaRuntimeAvailableAsync();
    Task<bool> IsInternetConnectionAvailableAsync();
}