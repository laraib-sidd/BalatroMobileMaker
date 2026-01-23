using BalatroMobile.Core.Models;

namespace BalatroMobile.Core.Services;

public class PatchService : IPatchService
{
    public async Task<IEnumerable<PatchResult>> ApplyPatchesAsync(IEnumerable<PatchConfig> patches, string basePath)
    {
        var results = new List<PatchResult>();

        foreach (var patch in patches)
        {
            try
            {
                var result = await ApplyPatchAsync(patch, basePath);
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(new PatchResult
                {
                    Success = false,
                    FilePath = patch.FilePath,
                    Description = patch.Description,
                    Error = ex.Message
                });
            }
        }

        return results;
    }

    public async Task<bool> ValidatePatchesAsync(IEnumerable<PatchConfig> patches, string basePath)
    {
        foreach (var patch in patches)
        {
            var filePath = Path.Combine(basePath, patch.FilePath);
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                if (!content.Contains(patch.SearchPattern))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private async Task<PatchResult> ApplyPatchAsync(PatchConfig patch, string basePath)
    {
        var filePath = Path.Combine(basePath, patch.FilePath);

        if (!File.Exists(filePath))
        {
            return new PatchResult
            {
                Success = false,
                FilePath = patch.FilePath,
                Description = patch.Description,
                Error = "File not found"
            };
        }

        var content = await File.ReadAllTextAsync(filePath);

        if (!content.Contains(patch.SearchPattern))
        {
            return new PatchResult
            {
                Success = false,
                FilePath = patch.FilePath,
                Description = patch.Description,
                Error = "Search pattern not found in file"
            };
        }

        // Find the first occurrence and replace it
        var index = content.IndexOf(patch.SearchPattern);
        if (index == -1)
        {
            return new PatchResult
            {
                Success = false,
                FilePath = patch.FilePath,
                Description = patch.Description,
                Error = "Search pattern not found"
            };
        }

        var newContent = content.Remove(index, patch.SearchPattern.Length)
                              .Insert(index, patch.Replacement);

        await File.WriteAllTextAsync(filePath, newContent);

        return new PatchResult
        {
            Success = true,
            FilePath = patch.FilePath,
            Description = patch.Description
        };
    }
}