using BalatroMobile.Core.Models;

namespace BalatroMobile.Core.Services;

/// <summary>
/// Patch service that applies patches by replacing ENTIRE LINES containing search patterns.
/// This matches the behavior of the original blake502/balatro-mobile-maker.
/// 
/// CRITICAL: The original tool replaces the ENTIRE LINE that contains the search pattern,
/// not just the search pattern itself. This is essential for the patches to work correctly.
/// </summary>
public class PatchService : IPatchService
{
    // #region agent log
    private static readonly string _debugLogPath = Path.Combine(Environment.CurrentDirectory, "debug.log");
    private static void DebugLog(string hypothesisId, string message, object? data = null)
    {
        try
        {
            var entry = System.Text.Json.JsonSerializer.Serialize(new { hypothesisId, location = "PatchService.cs", message, data, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sessionId = "debug-session" });
            File.AppendAllText(_debugLogPath, entry + "\n");
        }
        catch { }
    }
    // #endregion
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
                var lines = await File.ReadAllLinesAsync(filePath);
                bool found = lines.Any(line => line.Contains(patch.SearchPattern, StringComparison.Ordinal));
                if (!found)
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

    /// <summary>
    /// Applies a patch by finding the FIRST line containing the search pattern
    /// and replacing the ENTIRE LINE with the replacement text.
    /// 
    /// This matches the original blake502 tool behavior exactly.
    /// </summary>
    private async Task<PatchResult> ApplyPatchAsync(PatchConfig patch, string basePath)
    {
        var filePath = Path.Combine(basePath, patch.FilePath);

        // #region agent log
        DebugLog("E", $"ApplyPatch START: {patch.Description}", new { filePath, searchPattern = patch.SearchPattern, fileExists = File.Exists(filePath) });
        // #endregion

        if (!File.Exists(filePath))
        {
            // #region agent log
            DebugLog("E", $"ApplyPatch FILE NOT FOUND: {patch.Description}", new { filePath });
            // #endregion
            return new PatchResult
            {
                Success = false,
                FilePath = patch.FilePath,
                Description = patch.Description,
                Error = "File not found"
            };
        }

        // Read file as LINES (like the original tool)
        var lines = await File.ReadAllLinesAsync(filePath);
        
        // #region agent log
        DebugLog("E", $"ApplyPatch read file: {patch.FilePath}", new { totalLines = lines.Length });
        // #endregion
        
        // Find the FIRST line containing the search pattern
        bool found = false;
        int foundLineNum = -1;
        string? originalLine = null;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IndexOf(patch.SearchPattern, StringComparison.Ordinal) != -1)
            {
                foundLineNum = i;
                originalLine = lines[i];
                // Replace the ENTIRE LINE with the replacement text
                lines[i] = patch.Replacement;
                found = true;
                break; // Only replace the first occurrence
            }
        }

        // #region agent log
        DebugLog("E", $"ApplyPatch search result: {patch.Description}", new { found, foundLineNum, originalLine = originalLine?.Substring(0, Math.Min(100, originalLine?.Length ?? 0)), searchPattern = patch.SearchPattern });
        // #endregion

        if (!found)
        {
            return new PatchResult
            {
                Success = false,
                FilePath = patch.FilePath,
                Description = patch.Description,
                Error = $"Search pattern '{patch.SearchPattern}' not found in any line"
            };
        }

        // Write back as lines (like the original tool)
        await File.WriteAllLinesAsync(filePath, lines);

        // #region agent log
        DebugLog("E", $"ApplyPatch SUCCESS: {patch.Description}", new { foundLineNum });
        // #endregion

        return new PatchResult
        {
            Success = true,
            FilePath = patch.FilePath,
            Description = patch.Description
        };
    }
}