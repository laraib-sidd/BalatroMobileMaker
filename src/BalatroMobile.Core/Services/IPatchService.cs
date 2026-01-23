using BalatroMobile.Core.Models;

namespace BalatroMobile.Core.Services;

public interface IPatchService
{
    Task<IEnumerable<PatchResult>> ApplyPatchesAsync(IEnumerable<PatchConfig> patches, string basePath);
    Task<bool> ValidatePatchesAsync(IEnumerable<PatchConfig> patches, string basePath);
}