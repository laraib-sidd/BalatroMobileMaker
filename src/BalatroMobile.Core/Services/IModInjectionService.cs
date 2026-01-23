using BalatroMobile.Core.Models;

namespace BalatroMobile.Core.Services;

public interface IModInjectionService
{
    Task<ModInjectionResult> InjectModsAsync(ModInjectionConfig config);
    Task<bool> ValidateModInjectionAsync(ModInjectionConfig config);
    Task<IEnumerable<string>> GetAvailableModsAsync();
    Task<Dictionary<string, bool>> CheckModCompatibilityAsync(IEnumerable<string> modNames);
}