using System.Diagnostics;
using System.Text;

namespace BalatroMobile.Core.Services;

/// <summary>
/// Service for preparing and transferring mods to Android devices.
/// Handles creating stubs, packaging mods, and ADB transfer.
/// </summary>
public class ModTransferService
{
    private readonly Action<string>? _progressCallback;
    
    // Stub content for nativefs (Android-compatible wrapper for love.filesystem)
    // CRITICAL: This stub must resolve paths relative to the working directory
    // because SMODS sets working directory then reads with relative paths
    private const string NativefsStub = @"-- NativeFS stub for Android - with working directory support
local nfs = {}
local lf = love.filesystem
local _workingDir = """"

-- Helper to resolve paths relative to working directory
-- This is CRITICAL for SMODS asset loading!
local function resolvePath(path)
    if not path then return path end
    if path:sub(1,1) == ""/"" then return path end  -- absolute path
    if _workingDir ~= """" then
        return _workingDir .. ""/"" .. path
    end
    return path
end

function nfs.read(path)
    local resolved = resolvePath(path)
    local data = lf.read(resolved)
    if data then return data end
    -- Fallback to original path
    return lf.read(path)
end

function nfs.load(path)
    local resolved = resolvePath(path)
    local chunk = lf.load(resolved)
    if chunk then return chunk end
    return lf.load(path)
end

function nfs.getDirectoryItems(dir)
    local resolved = resolvePath(dir)
    local items = lf.getDirectoryItems(resolved)
    if items and #items > 0 then return items end
    return lf.getDirectoryItems(dir) or {}
end

function nfs.getDirectoryItemsInfo(dir, filtertype)
    local resolved = resolvePath(dir)
    local items = lf.getDirectoryItems(resolved)
    if not items or #items == 0 then
        items = lf.getDirectoryItems(dir) or {}
        resolved = dir
    end
    local result = {}
    for _, item in ipairs(items) do
        local fullpath = resolved .. ""/"" .. item
        local info = lf.getInfo(fullpath)
        if info then
            if not filtertype or info.type == filtertype then
                info.name = item
                table.insert(result, info)
            end
        end
    end
    return result
end

function nfs.getInfo(path)
    local resolved = resolvePath(path)
    local info = lf.getInfo(resolved)
    if info then return info end
    return lf.getInfo(path)
end

function nfs.setWorkingDirectory(dir)
    _workingDir = dir or """"
    return true
end

function nfs.getWorkingDirectory()
    if _workingDir == """" then return lf.getSaveDirectory() end
    return _workingDir
end

function nfs.write(path, data) return lf.write(resolvePath(path), data) end
function nfs.append(path, data) return lf.append(resolvePath(path), data) end
function nfs.createDirectory(path) return lf.createDirectory(resolvePath(path)) end
function nfs.remove(path) return lf.remove(resolvePath(path)) end
function nfs.getRealDirectory(path) return lf.getRealDirectory(resolvePath(path)) end
function nfs.getSaveDirectory() return lf.getSaveDirectory() end
function nfs.getSourceBaseDirectory() return lf.getSourceBaseDirectory and lf.getSourceBaseDirectory() or """" end

function nfs.isFile(path)
    local resolved = resolvePath(path)
    local info = lf.getInfo(resolved)
    if not info then info = lf.getInfo(path) end
    return info and info.type == ""file""
end

function nfs.isDirectory(path)
    local resolved = resolvePath(path)
    local info = lf.getInfo(resolved)
    if not info then info = lf.getInfo(path) end
    return info and info.type == ""directory""
end

function nfs.mount(archive, mountpoint, appendToPath) return lf.mount(archive, mountpoint, appendToPath) end
function nfs.unmount(archive) return lf.unmount(archive) end
function nfs.lines(path) return lf.lines(resolvePath(path)) end
function nfs.newFile(path, mode) return lf.newFile(resolvePath(path), mode) end
function nfs.newFileData(contents, name) return lf.newFileData(contents, name) end

return nfs";

    // Stub content for lovely module
    // CRITICAL: Must include 'path' field for SMODS compatibility
    private const string LovelyStub = @"-- Lovely stub for mobile
local lovely = {}
lovely.mod_dir = ""Mods""
lovely.path = ""Mods""  -- Required for SMODS.path to work
lovely.version = ""0.6.0""
return lovely";

    // Config file for lovely.lua
    private const string LovelyConfig = @"return {
    repo = ""https://github.com/ethangreen-dev/lovely-injector"",
    version = ""0.6.0"",
    mod_dir = ""/data/data/com.unofficial.balatro/files/save/game/Mods"",
}";

    public ModTransferService(Action<string>? progressCallback = null)
    {
        _progressCallback = progressCallback;
    }

    private void ReportProgress(string message)
    {
        _progressCallback?.Invoke(message);
        Console.WriteLine($"  {message}");
    }

    /// <summary>
    /// Prepares a mod package from the Balatro AppData folder.
    /// Creates all necessary stubs and config files.
    /// </summary>
    public async Task<ModPackageResult> PrepareModPackageAsync(string outputDir)
    {
        var result = new ModPackageResult();
        
        try
        {
            // Find Balatro AppData
            var balatroAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Balatro"
            );
            
            if (!Directory.Exists(balatroAppData))
            {
                result.Errors.Add($"Balatro AppData not found at: {balatroAppData}");
                return result;
            }
            
            var modsPath = Path.Combine(balatroAppData, "Mods");
            if (!Directory.Exists(modsPath))
            {
                result.Errors.Add($"Mods folder not found at: {modsPath}");
                return result;
            }
            
            var lovelyDumpPath = Path.Combine(modsPath, "lovely", "dump");
            if (!Directory.Exists(lovelyDumpPath))
            {
                result.Errors.Add($"Lovely dump not found at: {lovelyDumpPath}");
                result.Errors.Add("Run Balatro with mods on PC first to generate dump files.");
                return result;
            }
            
            ReportProgress($"Found Balatro mods at: {modsPath}");
            ReportProgress($"Found Lovely dump at: {lovelyDumpPath}");
            
            // Create output directory
            var gameDir = Path.Combine(outputDir, "game");
            if (Directory.Exists(gameDir))
            {
                Directory.Delete(gameDir, true);
            }
            Directory.CreateDirectory(gameDir);
            
            // Step 1: Copy Mods folder
            ReportProgress("Copying Mods folder...");
            var modsDestPath = Path.Combine(gameDir, "Mods");
            await CopyDirectoryAsync(modsPath, modsDestPath);
            result.Messages.Add("Copied Mods folder");
            
            // Step 2: Copy Lovely dump files to game root
            ReportProgress("Copying Lovely dump files...");
            foreach (var item in Directory.GetFileSystemEntries(lovelyDumpPath))
            {
                var destPath = Path.Combine(gameDir, Path.GetFileName(item));
                if (Directory.Exists(item))
                {
                    await CopyDirectoryAsync(item, destPath);
                }
                else
                {
                    File.Copy(item, destPath, true);
                }
            }
            result.Messages.Add("Copied Lovely dump files");
            
            // Step 3: Create SMODS folder with version.lua
            ReportProgress("Creating SMODS folder...");
            var smodsLibPath = Path.Combine(modsPath, "smods");
            var smodsDestPath = Path.Combine(gameDir, "SMODS");
            Directory.CreateDirectory(smodsDestPath);
            
            var versionLuaPath = Path.Combine(smodsLibPath, "version.lua");
            if (File.Exists(versionLuaPath))
            {
                File.Copy(versionLuaPath, Path.Combine(smodsDestPath, "version.lua"), true);
            }
            
            var releaseLuaPath = Path.Combine(smodsLibPath, "release.lua");
            if (File.Exists(releaseLuaPath))
            {
                File.Copy(releaseLuaPath, Path.Combine(smodsDestPath, "release.lua"), true);
            }
            result.Messages.Add("Created SMODS folder");
            
            // Step 4: Create nativefs stub
            // CRITICAL: Create BOTH nativefs.lua and nativefs/init.lua because
            // Lua's require searches for "nativefs.lua" BEFORE "nativefs/init.lua"
            ReportProgress("Creating nativefs stub...");
            var nativefsDir = Path.Combine(gameDir, "nativefs");
            Directory.CreateDirectory(nativefsDir);
            await File.WriteAllTextAsync(Path.Combine(nativefsDir, "init.lua"), NativefsStub);
            await File.WriteAllTextAsync(Path.Combine(gameDir, "nativefs.lua"), NativefsStub);
            result.Messages.Add("Created nativefs stubs");
            
            // Step 4b: Replace any FFI-based nativefs.lua files in mods
            // These use LuaJIT FFI which doesn't work on Android
            ReportProgress("Replacing FFI nativefs files in mods...");
            var nativefsFilesToReplace = Directory.GetFiles(modsDestPath, "nativefs.lua", SearchOption.AllDirectories);
            foreach (var nativefsFile in nativefsFilesToReplace)
            {
                try
                {
                    await File.WriteAllTextAsync(nativefsFile, NativefsStub);
                    result.Messages.Add($"Replaced {Path.GetRelativePath(gameDir, nativefsFile)}");
                }
                catch (Exception ex)
                {
                    result.Messages.Add($"Warning: Could not replace {nativefsFile}: {ex.Message}");
                }
            }
            
            // Step 5: Create lovely stub
            // CRITICAL: Create BOTH lovely.lua (config) and lovely/init.lua (module)
            ReportProgress("Creating lovely stub...");
            var lovelyDir = Path.Combine(gameDir, "lovely");
            Directory.CreateDirectory(lovelyDir);
            await File.WriteAllTextAsync(Path.Combine(lovelyDir, "init.lua"), LovelyStub);
            result.Messages.Add("Created lovely/init.lua stub");
            
            // Step 6: Create lovely.lua config
            ReportProgress("Creating lovely.lua config...");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "lovely.lua"), LovelyConfig);
            result.Messages.Add("Created lovely.lua config");
            
            // Step 7: Clean macOS metadata files
            ReportProgress("Cleaning macOS metadata files...");
            await CleanMacOsMetadataAsync(gameDir);
            result.Messages.Add("Cleaned ._* metadata files");
            
            result.OutputPath = gameDir;
            result.Success = true;
            ReportProgress("Mod package prepared successfully!");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to prepare mod package: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Transfers the prepared mod package to an Android device via ADB.
    /// </summary>
    public async Task<ModTransferResult> TransferToDeviceAsync(string modPackagePath)
    {
        var result = new ModTransferResult();
        
        try
        {
            // Check ADB is available
            if (!await IsAdbAvailableAsync())
            {
                result.Errors.Add("ADB not found. Please install Android SDK Platform Tools.");
                return result;
            }
            
            // Check device is connected
            if (!await IsDeviceConnectedAsync())
            {
                result.Errors.Add("No Android device connected. Please connect via USB and enable USB debugging.");
                return result;
            }
            
            ReportProgress("Android device connected");
            
            // Create tar archive
            ReportProgress("Creating transfer archive...");
            var tarPath = Path.Combine(Path.GetDirectoryName(modPackagePath)!, "mod-transfer.tar");
            if (File.Exists(tarPath))
            {
                File.Delete(tarPath);
            }
            
            var tarResult = await RunProcessAsync("tar", $"-cvf \"{tarPath}\" -C \"{modPackagePath}\" .");
            if (!tarResult.Success)
            {
                result.Errors.Add($"Failed to create tar archive: {tarResult.Error}");
                return result;
            }
            result.Messages.Add("Created transfer archive");
            
            // Push tar to device
            ReportProgress("Pushing archive to device...");
            var pushResult = await RunAdbAsync($"push \"{tarPath}\" /data/local/tmp/mod-transfer.tar");
            if (!pushResult.Success)
            {
                result.Errors.Add($"Failed to push archive: {pushResult.Error}");
                return result;
            }
            result.Messages.Add("Pushed archive to device");
            
            // Extract on device using run-as
            ReportProgress("Extracting on device...");
            var targetPath = "/data/data/com.unofficial.balatro/files/save/game";
            var extractCmd = $"run-as com.unofficial.balatro sh -c 'mkdir -p {targetPath} && cd {targetPath} && tar -xf /data/local/tmp/mod-transfer.tar'";
            var extractResult = await RunAdbAsync($"shell \"{extractCmd}\"");
            if (!extractResult.Success)
            {
                result.Errors.Add($"Failed to extract on device: {extractResult.Error}");
                return result;
            }
            result.Messages.Add("Extracted files on device");
            
            // Clean macOS metadata on device
            ReportProgress("Cleaning metadata files on device...");
            var cleanCmd = $"run-as com.unofficial.balatro find {targetPath} -name '._*' -type f -delete";
            await RunAdbAsync($"shell \"{cleanCmd}\"");
            result.Messages.Add("Cleaned metadata files");
            
            // Cleanup local tar
            if (File.Exists(tarPath))
            {
                File.Delete(tarPath);
            }
            
            // Verify
            ReportProgress("Verifying installation...");
            var verifyCmd = $"run-as com.unofficial.balatro ls {targetPath}/Mods/ 2>/dev/null";
            var verifyResult = await RunAdbAsync($"shell \"{verifyCmd}\"");
            if (!string.IsNullOrEmpty(verifyResult.Output))
            {
                result.Messages.Add($"Mods found: {verifyResult.Output.Trim()}");
            }
            
            result.Success = true;
            ReportProgress("Transfer completed successfully!");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Transfer failed: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Installs an APK on the connected Android device.
    /// </summary>
    public async Task<bool> InstallApkAsync(string apkPath)
    {
        if (!File.Exists(apkPath))
        {
            ReportProgress($"APK not found: {apkPath}");
            return false;
        }
        
        ReportProgress($"Installing APK: {Path.GetFileName(apkPath)}");
        var result = await RunAdbAsync($"install -r \"{apkPath}\"");
        
        if (result.Success && result.Output.Contains("Success"))
        {
            ReportProgress("APK installed successfully");
            return true;
        }
        
        ReportProgress($"APK installation failed: {result.Error}");
        return false;
    }

    /// <summary>
    /// Launches the Balatro app on the device.
    /// </summary>
    public async Task<bool> LaunchAppAsync()
    {
        ReportProgress("Launching Balatro...");
        var result = await RunAdbAsync("shell am start -n com.unofficial.balatro/org.love2d.android.GameActivity");
        return result.Success;
    }

    /// <summary>
    /// Force stops the Balatro app.
    /// </summary>
    public async Task<bool> StopAppAsync()
    {
        ReportProgress("Stopping Balatro...");
        var result = await RunAdbAsync("shell am force-stop com.unofficial.balatro");
        return result.Success;
    }

    private async Task<bool> IsAdbAvailableAsync()
    {
        try
        {
            var result = await RunProcessAsync("adb", "version");
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsDeviceConnectedAsync()
    {
        var result = await RunAdbAsync("devices");
        if (!result.Success) return false;
        
        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Any(l => l.Contains("device") && !l.Contains("List of devices"));
    }

    private async Task<ProcessResult> RunAdbAsync(string arguments)
    {
        return await RunProcessAsync("adb", arguments);
    }

    private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments)
    {
        var result = new ProcessResult();
        
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            process.Start();
            result.Output = await process.StandardOutput.ReadToEndAsync();
            result.Error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.Success = false;
        }
        
        return result;
    }

    private async Task CopyDirectoryAsync(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destSubDir);
        }
    }

    private async Task CleanMacOsMetadataAsync(string directory)
    {
        // Delete all ._* files (macOS AppleDouble metadata)
        var metadataFiles = Directory.GetFiles(directory, "._*", SearchOption.AllDirectories);
        foreach (var file in metadataFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch { }
        }
        
        // Delete .DS_Store files
        var dsStoreFiles = Directory.GetFiles(directory, ".DS_Store", SearchOption.AllDirectories);
        foreach (var file in dsStoreFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch { }
        }
        
        await Task.CompletedTask;
    }

    private class ProcessResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public int ExitCode { get; set; }
    }
}

public class ModPackageResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class ModTransferResult
{
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
