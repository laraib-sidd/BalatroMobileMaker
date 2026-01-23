using ICSharpCode.SharpZipLib.Zip;

namespace BalatroMobile.Infrastructure.Tools;

/// <summary>
/// Handles extraction of game content from Balatro.exe and creation of game.love files.
/// 
/// Balatro.exe is a LÖVE executable with game content appended as a ZIP archive.
/// The ZIP uses LZMA compression which requires SharpZipLib (System.IO.Compression doesn't support it).
/// </summary>
public class GameExtractor
{
    private readonly Action<string>? _progressCallback;

    public GameExtractor(Action<string>? progressCallback = null)
    {
        _progressCallback = progressCallback;
    }

    /// <summary>
    /// Extracts game content from Balatro.exe to a directory.
    /// Balatro.exe has ZIP data appended to the end of the executable.
    /// </summary>
    public async Task<bool> ExtractGameAsync(string balatroExePath, string extractPath)
    {
        if (!File.Exists(balatroExePath))
        {
            ReportProgress($"Balatro.exe not found at: {balatroExePath}");
            return false;
        }

        try
        {
            Directory.CreateDirectory(extractPath);

            // First, check if there's already a Game.love file (some users have this)
            var gameLovePath = Path.Combine(Path.GetDirectoryName(balatroExePath)!, "Game.love");
            if (File.Exists(gameLovePath))
            {
                ReportProgress("Found Game.love, extracting from it...");
                return await ExtractLoveFileAsync(gameLovePath, extractPath);
            }

            // Balatro.exe is a LÖVE fused executable - it has ZIP data appended to the end
            // We need to find where the ZIP starts (look for PK signature)
            ReportProgress("Extracting game content from Balatro.exe...");
            
            var exeBytes = await File.ReadAllBytesAsync(balatroExePath);
            var zipStartOffset = FindZipStartOffset(exeBytes);
            
            if (zipStartOffset == -1)
            {
                ReportProgress("Could not find ZIP data in Balatro.exe");
                return false;
            }

            ReportProgress($"Found ZIP data at offset {zipStartOffset}");

            // Extract just the ZIP portion to a temp file
            var tempZipPath = Path.Combine(Path.GetTempPath(), "balatro_temp.zip");
            try
            {
                await using (var fs = File.Create(tempZipPath))
                {
                    await fs.WriteAsync(exeBytes, zipStartOffset, exeBytes.Length - zipStartOffset);
                }

                // Now extract the ZIP using SharpZipLib
                var fastZip = new FastZip();
                fastZip.ExtractZip(tempZipPath, extractPath, null);
                
                // Verify extraction worked
                var luaFiles = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories);
                if (luaFiles.Length == 0)
                {
                    ReportProgress("Warning: No Lua files found after extraction");
                    return false;
                }

                ReportProgress($"Extracted {luaFiles.Length} Lua files");
                return true;
            }
            finally
            {
                // Cleanup temp file
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            ReportProgress($"Extraction failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Extracts content from a .love file (which is just a ZIP file).
    /// </summary>
    public async Task<bool> ExtractLoveFileAsync(string lovePath, string extractPath)
    {
        if (!File.Exists(lovePath))
        {
            ReportProgress($"Love file not found: {lovePath}");
            return false;
        }

        try
        {
            Directory.CreateDirectory(extractPath);
            
            var fastZip = new FastZip();
            fastZip.ExtractZip(lovePath, extractPath, null);
            
            var luaFiles = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories);
            ReportProgress($"Extracted {luaFiles.Length} Lua files from .love");
            
            return luaFiles.Length > 0;
        }
        catch (Exception ex)
        {
            ReportProgress($"Love file extraction failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a game.love file from extracted/patched game content.
    /// A .love file is simply a ZIP file with .love extension.
    /// </summary>
    public async Task<bool> CreateGameLoveAsync(string sourceDir, string outputPath)
    {
        if (!Directory.Exists(sourceDir))
        {
            ReportProgress($"Source directory not found: {sourceDir}");
            return false;
        }

        try
        {
            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Delete existing file if present
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            ReportProgress("Creating game.love from extracted content...");

            var fastZip = new FastZip();
            fastZip.CreateZip(outputPath, sourceDir, true, null);

            if (File.Exists(outputPath))
            {
                var fileInfo = new FileInfo(outputPath);
                ReportProgress($"Created game.love: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            ReportProgress($"Failed to create game.love: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Finds the start of ZIP data in a LÖVE fused executable.
    /// LÖVE appends the ZIP data to the end of the executable.
    /// ZIP files start with "PK" (0x50 0x4B).
    /// </summary>
    private int FindZipStartOffset(byte[] data)
    {
        // ZIP local file header signature: PK\x03\x04
        byte[] zipSignature = { 0x50, 0x4B, 0x03, 0x04 };
        
        // Start searching from a reasonable offset (skip the PE header)
        // LÖVE executables are usually around 3-5MB, game data starts after
        int startSearch = Math.Min(1024 * 1024, data.Length / 2); // Start at 1MB or half
        
        for (int i = startSearch; i < data.Length - 4; i++)
        {
            if (data[i] == zipSignature[0] &&
                data[i + 1] == zipSignature[1] &&
                data[i + 2] == zipSignature[2] &&
                data[i + 3] == zipSignature[3])
            {
                return i;
            }
        }

        // If not found in second half, try from beginning (smaller games)
        for (int i = 0; i < startSearch && i < data.Length - 4; i++)
        {
            if (data[i] == zipSignature[0] &&
                data[i + 1] == zipSignature[1] &&
                data[i + 2] == zipSignature[2] &&
                data[i + 3] == zipSignature[3])
            {
                return i;
            }
        }

        return -1;
    }

    private void ReportProgress(string message)
    {
        _progressCallback?.Invoke(message);
    }
}
