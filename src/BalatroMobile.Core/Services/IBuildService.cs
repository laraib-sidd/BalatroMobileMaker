using BalatroMobile.Core.Models;

namespace BalatroMobile.Core.Services;

public interface IBuildService
{
    Task<BuildResult> BuildAsync(BuildConfig config, IProgress<string>? progress = null);
    Task<bool> ValidateBuildEnvironmentAsync();
    IEnumerable<string> GetSupportedPlatforms();
}