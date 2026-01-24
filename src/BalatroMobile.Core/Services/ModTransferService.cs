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
    private readonly string? _adbPath;
    
    // Stub content for nativefs (Android-compatible wrapper for love.filesystem)
    // CRITICAL: This stub handles all nativefs API signatures including:
    // - read(path), read(mode, path), read(mode, path, size)
    // - When mode='data', returns FileData not string
    // - newFileData(path) and newFileData(contents, name)
    private const string NativefsStub = @"-- NativeFS stub for Android - WORKING VERSION
-- Handles all nativefs API signatures for SMODS compatibility
local nfs = {}
local lf = love.filesystem
local _workingDir = """"

-- read: handles nativefs.read(path), nativefs.read(mode, path), nativefs.read(mode, path, size)
-- mode can be 'string' (default) or 'data' (returns FileData)
function nfs.read(arg1, arg2, arg3)
    local container, name, size
    if arg3 ~= nil then
        container, name, size = arg1, arg2, arg3
    elseif arg2 == nil then
        container, name, size = 'string', arg1, 'all'
    else
        if type(arg2) == 'number' or arg2 == 'all' then
            container, name, size = 'string', arg1, arg2
        else
            container, name, size = arg1, arg2, 'all'
        end
    end
    
    local contents, bytes = lf.read(name)
    if not contents then return nil, 0 end
    
    if container == 'data' then
        -- Return FileData instead of string (required for sound loading)
        local filename = name:match('[^/]+$') or name
        return lf.newFileData(contents, filename), bytes
    end
    return contents, bytes
end

function nfs.load(path) return lf.load(path) end
function nfs.getDirectoryItems(dir) return lf.getDirectoryItems(dir) or {} end

function nfs.getDirectoryItemsInfo(dir, filtertype)
    local items = nfs.getDirectoryItems(dir)
    local result = {}
    for _, item in ipairs(items) do
        local itemPath = dir .. '/' .. item
        local info = lf.getInfo(itemPath)
        if info and (not filtertype or info.type == filtertype) then
            info.name = item
            table.insert(result, info)
        end
    end
    return result
end

function nfs.getInfo(path) return lf.getInfo(path) end
function nfs.setWorkingDirectory(dir) _workingDir = dir or ''; return true end
function nfs.getWorkingDirectory() return _workingDir end
function nfs.write(path, data, size) return lf.write(path, data, size) end
function nfs.append(path, data, size) return lf.append(path, data, size) end
function nfs.createDirectory(path) return lf.createDirectory(path) end
function nfs.remove(path) return lf.remove(path) end
function nfs.getSaveDirectory() return lf.getSaveDirectory() end
function nfs.getSourceBaseDirectory() return '' end
function nfs.isFile(path) local i = lf.getInfo(path); return i and i.type == 'file' end
function nfs.isDirectory(path) local i = lf.getInfo(path); return i and i.type == 'directory' end
function nfs.lines(path) return lf.lines(path) end
function nfs.newFile(path, mode) return lf.newFile(path, mode) end

-- newFileData: support both forms - (path) or (contents, name)
function nfs.newFileData(arg1, arg2)
    if arg2 then
        -- Two args: contents, name
        return lf.newFileData(arg1, arg2)
    else
        -- One arg: filepath - read and create FileData
        local contents = lf.read(arg1)
        if not contents then return nil end
        local name = arg1:match('[^/]+$') or arg1
        return lf.newFileData(contents, name)
    end
end

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
    // NOTE: With t.identity = "Balatro" in conf.lua, save path is save/Balatro/
    private const string LovelyConfig = @"return {
    repo = ""https://github.com/ethangreen-dev/lovely-injector"",
    version = ""0.6.0"",
    mod_dir = ""/data/data/com.unofficial.balatro/files/save/Balatro/Mods"",
}";

    public ModTransferService(Action<string>? progressCallback = null, string? adbPath = null)
    {
        _progressCallback = progressCallback;
        _adbPath = adbPath;
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
            
            // Check for BalatroMobileCompat - CRITICAL for mobile mod compatibility
            var mobileCompatPath = Path.Combine(modsPath, "BalatroMobileCompat");
            if (!Directory.Exists(mobileCompatPath))
            {
                result.Errors.Add("BalatroMobileCompat not found!");
                result.Errors.Add("This mod is REQUIRED for mods to work on mobile.");
                result.Errors.Add("Download from: https://github.com/eeve-lyn/BalatroMobileCompat");
                result.Errors.Add($"Install to: {mobileCompatPath}");
                return result;
            }
            ReportProgress("Found BalatroMobileCompat (required for mobile)");
            
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
            // Run ADB diagnostics
            var diagResult = await RunAdbDiagnosticsAsync();
            
            if (!diagResult.AdbInstalled)
            {
                result.Errors.Add("ADB not found. See instructions above.");
                return result;
            }
            
            if (!diagResult.DeviceConnected)
            {
                result.Errors.Add("No Android device detected. Follow the troubleshooting steps above.");
                return result;
            }
            
            if (!diagResult.DeviceAuthorized)
            {
                result.Errors.Add("Device not authorized. Check your phone for the authorization prompt.");
                return result;
            }
            
            Console.WriteLine();
            ReportProgress("ADB connection verified");
            
            // Create tar archive
            // Note: This can take 30-60 seconds for large mod folders
            var modFolderSize = GetDirectorySize(modPackagePath);
            ReportProgress($"Creating transfer archive ({modFolderSize / 1024.0 / 1024.0:F1} MB)...");
            ReportProgress("  This may take 30-60 seconds, please wait...");
            
            var tarPath = Path.Combine(Path.GetDirectoryName(modPackagePath)!, "mod-transfer.tar");
            if (File.Exists(tarPath))
            {
                File.Delete(tarPath);
            }
            
            // Use quiet mode (-cf not -cvf) to avoid flooding output
            var tarResult = await RunProcessAsync("tar", $"-cf \"{tarPath}\" -C \"{modPackagePath}\" .");
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
            // NOTE: With t.identity = "Balatro" in conf.lua, save path is save/Balatro/
            ReportProgress("Extracting on device...");
            var targetPath = "/data/data/com.unofficial.balatro/files/save/Balatro";
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

    /// <summary>
    /// Comprehensive ADB diagnostics with troubleshooting guidance.
    /// </summary>
    public async Task<AdbDiagnosticResult> RunAdbDiagnosticsAsync()
    {
        var result = new AdbDiagnosticResult();
        
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              ADB CONNECTION DIAGNOSTICS                      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Step 1: Check if ADB is installed
        Console.WriteLine("▶ Checking ADB installation...");
        var adbVersion = await RunProcessAsync("adb", "version");
        if (!adbVersion.Success)
        {
            result.AdbInstalled = false;
            Console.WriteLine("  ✗ ADB not found!");
            Console.WriteLine();
            Console.WriteLine("  ╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  HOW TO INSTALL ADB:                                       ║");
            Console.WriteLine("  ╠════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Windows:                                                  ║");
            Console.WriteLine("  ║    1. Download: platform-tools from developer.android.com ║");
            Console.WriteLine("  ║    2. Extract to C:\\platform-tools                         ║");
            Console.WriteLine("  ║    3. Add to PATH or run from that folder                  ║");
            Console.WriteLine("  ║                                                            ║");
            Console.WriteLine("  ║  macOS:                                                    ║");
            Console.WriteLine("  ║    brew install android-platform-tools                     ║");
            Console.WriteLine("  ║                                                            ║");
            Console.WriteLine("  ║  Linux:                                                    ║");
            Console.WriteLine("  ║    sudo apt install adb                                    ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════════╝");
            return result;
        }
        
        result.AdbInstalled = true;
        var versionLine = adbVersion.Output.Split('\n').FirstOrDefault() ?? adbVersion.Error.Split('\n').FirstOrDefault();
        Console.WriteLine($"  ✓ ADB installed: {versionLine?.Trim()}");

        // Step 2: Check for connected devices
        Console.WriteLine();
        Console.WriteLine("▶ Checking for connected devices...");
        var devicesResult = await RunAdbAsync("devices -l");
        
        if (!devicesResult.Success)
        {
            Console.WriteLine("  ✗ Failed to query devices");
            return result;
        }

        var deviceLines = devicesResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith("List of devices"))
            .ToList();

        if (deviceLines.Count == 0)
        {
            result.DeviceConnected = false;
            Console.WriteLine("  ✗ No devices found!");
            Console.WriteLine();
            ShowDeviceNotFoundHelp();
            return result;
        }

        // Parse device status
        foreach (var line in deviceLines)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var deviceId = parts[0];
                var status = parts[1];
                
                Console.WriteLine($"  Device: {deviceId}");
                Console.WriteLine($"  Status: {status}");
                
                if (status == "unauthorized")
                {
                    result.DeviceConnected = true;
                    result.DeviceAuthorized = false;
                    Console.WriteLine();
                    Console.WriteLine("  ╔════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("  ║  DEVICE NOT AUTHORIZED!                                    ║");
                    Console.WriteLine("  ╠════════════════════════════════════════════════════════════╣");
                    Console.WriteLine("  ║  1. Look at your phone screen                              ║");
                    Console.WriteLine("  ║  2. You should see 'Allow USB debugging?' prompt           ║");
                    Console.WriteLine("  ║  3. Check 'Always allow from this computer'                ║");
                    Console.WriteLine("  ║  4. Tap 'Allow'                                            ║");
                    Console.WriteLine("  ║  5. Run this tool again                                    ║");
                    Console.WriteLine("  ╚════════════════════════════════════════════════════════════╝");
                    return result;
                }
                else if (status == "offline")
                {
                    result.DeviceConnected = true;
                    result.DeviceAuthorized = false;
                    Console.WriteLine();
                    Console.WriteLine("  ╔════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("  ║  DEVICE OFFLINE!                                           ║");
                    Console.WriteLine("  ╠════════════════════════════════════════════════════════════╣");
                    Console.WriteLine("  ║  Try these steps:                                          ║");
                    Console.WriteLine("  ║  1. Unplug and replug the USB cable                        ║");
                    Console.WriteLine("  ║  2. Try a different USB port                               ║");
                    Console.WriteLine("  ║  3. Run: adb kill-server && adb start-server               ║");
                    Console.WriteLine("  ║  4. Toggle USB debugging off and on                        ║");
                    Console.WriteLine("  ╚════════════════════════════════════════════════════════════╝");
                    return result;
                }
                else if (status == "device")
                {
                    result.DeviceConnected = true;
                    result.DeviceAuthorized = true;
                    
                    // Get device info
                    var modelResult = await RunAdbAsync("shell getprop ro.product.model");
                    var androidResult = await RunAdbAsync("shell getprop ro.build.version.release");
                    
                    if (modelResult.Success)
                        Console.WriteLine($"  Model: {modelResult.Output.Trim()}");
                    if (androidResult.Success)
                        Console.WriteLine($"  Android: {androidResult.Output.Trim()}");
                    
                    Console.WriteLine("  ✓ Device ready!");
                }
            }
        }

        // Step 3: Check if Balatro is installed
        if (result.DeviceAuthorized)
        {
            Console.WriteLine();
            Console.WriteLine("▶ Checking for Balatro installation...");
            var packageResult = await RunAdbAsync("shell pm list packages com.unofficial.balatro");
            
            if (packageResult.Output.Contains("com.unofficial.balatro"))
            {
                result.BalatroInstalled = true;
                Console.WriteLine("  ✓ Balatro is installed");
                
                // Check save directory
                var saveCheck = await RunAdbAsync("shell run-as com.unofficial.balatro ls /data/data/com.unofficial.balatro/files/save/Balatro/Mods 2>/dev/null");
                if (saveCheck.Success && !string.IsNullOrWhiteSpace(saveCheck.Output))
                {
                    result.ModsPresent = true;
                    var modCount = saveCheck.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                    Console.WriteLine($"  ✓ Mods folder exists ({modCount} items)");
                }
                else
                {
                    Console.WriteLine("  ○ Mods folder not found (will be created on transfer)");
                }
            }
            else
            {
                result.BalatroInstalled = false;
                Console.WriteLine("  ○ Balatro not installed yet");
            }
        }

        Console.WriteLine();
        if (result.IsReady)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ✓ ALL CHECKS PASSED - Ready for mod transfer!              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        }
        
        return result;
    }

    private void ShowDeviceNotFoundHelp()
    {
        Console.WriteLine("  ╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  DEVICE NOT SHOWING? TRY THESE STEPS:                      ║");
        Console.WriteLine("  ╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine("  ║                                                            ║");
        Console.WriteLine("  ║  1. ENABLE USB DEBUGGING:                                  ║");
        Console.WriteLine("  ║     Settings → About Phone → Tap 'Build Number' 7 times   ║");
        Console.WriteLine("  ║     Settings → Developer Options → Enable USB Debugging   ║");
        Console.WriteLine("  ║                                                            ║");
        Console.WriteLine("  ║  2. CHECK USB CONNECTION MODE:                             ║");
        Console.WriteLine("  ║     When you plug in, select 'File Transfer' or 'MTP'     ║");
        Console.WriteLine("  ║     (NOT 'Charging only')                                  ║");
        Console.WriteLine("  ║                                                            ║");
        Console.WriteLine("  ║  3. TRY DIFFERENT USB CABLE/PORT:                          ║");
        Console.WriteLine("  ║     Some cables are charge-only (no data)                  ║");
        Console.WriteLine("  ║     Try the cable that came with your phone                ║");
        Console.WriteLine("  ║                                                            ║");
        Console.WriteLine("  ║  4. RESTART ADB SERVER:                                    ║");
        Console.WriteLine("  ║     Open terminal/cmd and run:                             ║");
        Console.WriteLine("  ║       adb kill-server                                      ║");
        Console.WriteLine("  ║       adb start-server                                     ║");
        Console.WriteLine("  ║       adb devices                                          ║");
        Console.WriteLine("  ║                                                            ║");
        Console.WriteLine("  ║  5. INSTALL USB DRIVERS (Windows only):                    ║");
        Console.WriteLine("  ║     Download from your phone manufacturer's website        ║");
        Console.WriteLine("  ║     Or use Universal ADB Driver                            ║");
        Console.WriteLine("  ║                                                            ║");
        Console.WriteLine("  ║  6. CHECK FOR AUTHORIZATION PROMPT:                        ║");
        Console.WriteLine("  ║     Look at your phone - tap 'Allow' if prompted           ║");
        Console.WriteLine("  ║                                                            ║");
        Console.WriteLine("  ╚════════════════════════════════════════════════════════════╝");
    }

    private async Task<ProcessResult> RunAdbAsync(string arguments)
    {
        // Use bundled ADB if path was provided, otherwise use system ADB
        var adbExecutable = _adbPath ?? "adb";
        return await RunProcessAsync(adbExecutable, arguments);
    }

    private long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
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

public class AdbDiagnosticResult
{
    public bool AdbInstalled { get; set; }
    public bool DeviceConnected { get; set; }
    public bool DeviceAuthorized { get; set; }
    public bool BalatroInstalled { get; set; }
    public bool ModsPresent { get; set; }
    
    /// <summary>
    /// True if ADB is installed, device is connected and authorized.
    /// </summary>
    public bool IsReady => AdbInstalled && DeviceConnected && DeviceAuthorized;
}
