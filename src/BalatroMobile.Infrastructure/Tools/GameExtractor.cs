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
    /// Extracts game content from Balatro.exe or Game.love to a directory.
    /// Balatro.exe has ZIP data appended to the end of the executable.
    /// </summary>
    public async Task<bool> ExtractGameAsync(string gameFilePath, string extractPath)
    {
        if (!File.Exists(gameFilePath))
        {
            ReportProgress($"Game file not found at: {gameFilePath}");
            return false;
        }

        var fileInfo = new FileInfo(gameFilePath);
        ReportProgress($"Processing: {fileInfo.Name} ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

        try
        {
            Directory.CreateDirectory(extractPath);

            // Check if it's a .love file (which is just a ZIP)
            if (gameFilePath.EndsWith(".love", StringComparison.OrdinalIgnoreCase))
            {
                ReportProgress("Extracting from Game.love...");
                return await ExtractLoveFileAsync(gameFilePath, extractPath);
            }

            // Also check for Game.love in same directory
            var gameLovePath = Path.Combine(Path.GetDirectoryName(gameFilePath)!, "Game.love");
            if (File.Exists(gameLovePath))
            {
                ReportProgress("Found Game.love in same folder, using that instead...");
                return await ExtractLoveFileAsync(gameLovePath, extractPath);
            }

            // Try to extract Balatro.exe directly as a ZIP first
            // This matches the original tool: extractZip("Balatro.exe", "Balatro");
            ReportProgress("Trying direct ZIP extraction (like original tool)...");
            
            try
            {
                var fastZip = new FastZip();
                fastZip.ExtractZip(gameFilePath, extractPath, null);
                
                var luaFiles = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories);
                
                if (luaFiles.Length > 0)
                {
                    ReportProgress($"Direct extraction successful! Found {luaFiles.Length} Lua files");
                    return true;
                }
                
                // Clear failed extraction
                foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch (Exception ex)
            {
                ReportProgress($"Direct extraction failed: {ex.Message}, trying offset search...");
            }

            // Balatro.exe is a LÖVE fused executable - it has ZIP data appended
            ReportProgress("Reading executable to find embedded game data...");
            
            var exeBytes = await File.ReadAllBytesAsync(gameFilePath);
            ReportProgress($"Read {exeBytes.Length:N0} bytes");
            
            var zipStartOffset = FindZipStartOffset(exeBytes);
            
            if (zipStartOffset == -1)
            {
                ReportProgress("ERROR: Could not find ZIP data in executable");
                ReportProgress("The file might be corrupted or in an unsupported format.");
                ReportProgress("Try copying a fresh Balatro.exe from Steam.");
                return false;
            }

            var zipSize = exeBytes.Length - zipStartOffset;
            ReportProgress($"Extracting {zipSize / 1024.0 / 1024.0:F2} MB of game data...");

            // Extract just the ZIP portion to a temp file
            var tempZipPath = Path.Combine(Path.GetTempPath(), $"balatro_extract_{Guid.NewGuid()}.zip");
            try
            {
                await using (var fs = File.Create(tempZipPath))
                {
                    await fs.WriteAsync(exeBytes, zipStartOffset, zipSize);
                }
                
                ReportProgress($"Created temp ZIP: {tempZipPath}");

                // Now extract the ZIP using SharpZipLib
                var fastZip = new FastZip();
                fastZip.ExtractZip(tempZipPath, extractPath, null);
                
                // Verify extraction worked
                var luaFiles = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories);
                if (luaFiles.Length == 0)
                {
                    ReportProgress("ERROR: No Lua files found after extraction");
                    ReportProgress("The ZIP data might be corrupted or in wrong format.");
                    return false;
                }

                ReportProgress($"SUCCESS: Extracted {luaFiles.Length} Lua files");
                return true;
            }
            catch (Exception ex)
            {
                ReportProgress($"ZIP extraction failed: {ex.Message}");
                return false;
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
            ReportProgress($"Stack: {ex.StackTrace}");
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
        
        ReportProgress($"Searching for ZIP signature in {data.Length:N0} bytes...");
        
        // Search from the beginning - the ZIP might be anywhere
        // We want the LAST occurrence since game data is appended to the end
        int lastFound = -1;
        
        for (int i = 0; i < data.Length - 4; i++)
        {
            if (data[i] == zipSignature[0] &&
                data[i + 1] == zipSignature[1] &&
                data[i + 2] == zipSignature[2] &&
                data[i + 3] == zipSignature[3])
            {
                // Verify this looks like a valid ZIP by checking for common patterns
                // The filename length is at offset 26-27 from the signature
                if (i + 30 < data.Length)
                {
                    int filenameLen = data[i + 26] | (data[i + 27] << 8);
                    // Sanity check: filename should be reasonable length
                    if (filenameLen > 0 && filenameLen < 256)
                    {
                        lastFound = i;
                        // Don't break - we want the last valid ZIP (in case there are multiple)
                    }
                }
            }
        }
        
        if (lastFound != -1)
        {
            ReportProgress($"Found ZIP signature at offset {lastFound:N0}");
        }
        
        return lastFound;
    }

    private void ReportProgress(string message)
    {
        _progressCallback?.Invoke(message);
    }
}
