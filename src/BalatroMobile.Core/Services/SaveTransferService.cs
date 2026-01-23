using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services.GameDetection;
using System.Diagnostics;

namespace BalatroMobile.Core.Services;

public class SaveTransferService : ISaveTransferService
{
    private readonly IPlatformDetector _platformDetector;

    public SaveTransferService(IPlatformDetector platformDetector)
    {
        _platformDetector = platformDetector;
    }

    public async Task<SaveTransferResult> TransferSavesAsync(SaveTransferConfig config)
    {
        var startTime = DateTime.Now;

        // Validate environment first
        if (!await ValidateTransferEnvironmentAsync(config))
        {
            return new SaveTransferResult
            {
                Success = false,
                Direction = config.Direction,
                Errors = new[] { "Transfer environment validation failed" },
                Duration = DateTime.Now - startTime
            };
        }

        // Create backup if requested
        string? backupPath = null;
        if (config.CreateBackup)
        {
            var sourcePath = GetSourcePath(config);
            backupPath = await CreateBackupAsync(sourcePath);
            if (backupPath == null)
            {
                return new SaveTransferResult
                {
                    Success = false,
                    Direction = config.Direction,
                    Errors = new[] { "Failed to create backup" },
                    Duration = DateTime.Now - startTime
                };
            }
        }

        // Perform the transfer
        var result = config.Direction == TransferDirection.PcToAndroid
            ? await TransferPcToAndroidAsync(config)
            : await TransferAndroidToPcAsync(config);

        return result with
        {
            BackupPath = backupPath,
            Duration = DateTime.Now - startTime
        };
    }

    public async Task<bool> ValidateTransferEnvironmentAsync(SaveTransferConfig config)
    {
        // Check ADB connection
        if (!await _platformDetector.IsADBConnectionWorkingAsync())
            return false;

        // Check source path exists
        var sourcePath = GetSourcePath(config);
        if (!Directory.Exists(sourcePath))
            return false;

        // For PC to Android, check that target files exist
        if (config.Direction == TransferDirection.PcToAndroid)
        {
            var requiredFiles = config.FilesToTransfer.Where(file =>
                !config.ExcludedFiles.Contains(file));

            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(sourcePath, file);
                if (!File.Exists(filePath))
                {
                    // File doesn't exist, but that's okay - we'll just skip it
                }
            }
        }

        return true;
    }

    public async Task<IEnumerable<string>> GetAvailableSaveFilesAsync()
    {
        var savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Balatro");

        if (!Directory.Exists(savePath))
            return Array.Empty<string>();

        var availableFiles = new List<string>();

        // Check for save files in different slots
        for (int slot = 1; slot <= 3; slot++)
        {
            var slotPath = Path.Combine(savePath, slot.ToString());
            if (Directory.Exists(slotPath))
            {
                var files = Directory.GetFiles(slotPath, "*.jkr");
                availableFiles.AddRange(files.Select(f => Path.GetFileName(f)));
            }
        }

        // Check for global settings
        var settingsFile = Path.Combine(savePath, "settings.jkr");
        if (File.Exists(settingsFile))
        {
            availableFiles.Add("settings.jkr");
        }

        return availableFiles.Distinct();
    }

    public async Task<string?> CreateBackupAsync(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
            return null;

        var backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BalatroMobile",
            "Backups",
            $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");

        Directory.CreateDirectory(backupDir);

        try
        {
            // Copy all files recursively
            foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourcePath, file);
                var targetPath = Path.Combine(backupDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath, true);
            }

            return backupDir;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RestoreBackupAsync(string backupPath, string targetPath)
    {
        if (!Directory.Exists(backupPath) || !Directory.Exists(targetPath))
            return false;

        try
        {
            foreach (var file in Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(backupPath, file);
                var targetFile = Path.Combine(targetPath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetSourcePath(SaveTransferConfig config)
    {
        return config.SourcePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Balatro");
    }

    private async Task<SaveTransferResult> TransferPcToAndroidAsync(SaveTransferConfig config)
    {
        var sourcePath = GetSourcePath(config);
        var transferredFiles = new List<string>();
        var errors = new List<string>();
        var totalBytes = 0L;

        // Create temporary directory on Android
        var tempDir = $"/data/local/tmp/balatro_transfer_{Guid.NewGuid():N}";
        await ExecuteAdbCommandAsync($"shell mkdir -p {tempDir}");

        string? tempDirRef = tempDir; // Capture for finally block
        try
        {
            // Transfer each file
            foreach (var file in config.FilesToTransfer.Where(f => !config.ExcludedFiles.Contains(f)))
            {
                var sourceFile = Path.Combine(sourcePath, file);
                var androidPath = $"{tempDir}/{file.Replace('\\', '/')}";

                if (File.Exists(sourceFile))
                {
                    // Create directory structure on Android
                    var androidDir = Path.GetDirectoryName(androidPath)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(androidDir))
                    {
                        await ExecuteAdbCommandAsync($"shell mkdir -p {androidDir}");
                    }

                    // Push file to Android
                    var pushResult = await ExecuteAdbCommandAsync($"push \"{sourceFile}\" \"{androidPath}\"");
                    if (pushResult.Contains("error") || pushResult.Contains("failed"))
                    {
                        errors.Add($"Failed to transfer {file}: {pushResult}");
                    }
                    else
                    {
                        transferredFiles.Add(file);
                        totalBytes += new FileInfo(sourceFile).Length;
                    }
                }
            }

            // Move files to final Android app location
            var appDataDir = $"/data/data/{config.TargetAppPackage}/files/save/game";
            await ExecuteAdbCommandAsync($"shell mkdir -p {appDataDir}");

            // Force stop app before transferring
            await ExecuteAdbCommandAsync($"shell am force-stop {config.TargetAppPackage}");

            // Copy files to app directory
            await ExecuteAdbCommandAsync($"shell cp -r {tempDir}/* {appDataDir}/");

            // Set proper permissions
            await ExecuteAdbCommandAsync($"shell chown -R {config.TargetAppPackage}:{config.TargetAppPackage} {appDataDir}");

            return new SaveTransferResult
            {
                Success = transferredFiles.Any(),
                Direction = TransferDirection.PcToAndroid,
                FilesTransferred = transferredFiles.Count,
                BytesTransferred = totalBytes,
                TransferredFiles = transferredFiles,
                Errors = errors
            };
        }
        finally
        {
            // Clean up temporary directory
            if (tempDirRef != null)
            {
                await ExecuteAdbCommandAsync($"shell rm -rf {tempDirRef}");
            }
        }
    }

    private async Task<SaveTransferResult> TransferAndroidToPcAsync(SaveTransferConfig config)
    {
        var targetPath = GetSourcePath(config);
        var transferredFiles = new List<string>();
        var errors = new List<string>();
        var totalBytes = 0L;

        // Create backup first
        var backupPath = await CreateBackupAsync(targetPath);
        if (backupPath == null)
        {
            errors.Add("Failed to create backup before transfer");
        }

        var appDataDir = $"/data/data/{config.TargetAppPackage}/files/save/game";
        string? tempDirRef = null;

        try
        {
            // Create temporary directory on Android for pulling
            var tempDir = $"/data/local/tmp/balatro_pull_{Guid.NewGuid():N}";
            await ExecuteAdbCommandAsync($"shell mkdir -p {tempDir}");

            tempDirRef = tempDir; // Capture for finally block

            // Copy files from app directory to temp directory
            foreach (var file in config.FilesToTransfer.Where(f => !config.ExcludedFiles.Contains(f)))
            {
                var androidPath = $"{appDataDir}/{file.Replace('\\', '/')}";
                var tempPath = $"{tempDir}/{Path.GetFileName(file)}";

                var copyResult = await ExecuteAdbCommandAsync($"shell \"[ -f {androidPath} ] && cp {androidPath} {tempPath} || echo 'File not found'\"");
                if (!copyResult.Contains("File not found"))
                {
                    // Pull file from Android
                    var localTempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    var pullResult = await ExecuteAdbCommandAsync($"pull {tempPath} \"{localTempPath}\"");

                    if (!pullResult.Contains("error") && File.Exists(localTempPath))
                    {
                        // Move to final location
                        var finalPath = Path.Combine(targetPath, file);
                        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                        File.Move(localTempPath, finalPath, true);

                        transferredFiles.Add(file);
                        totalBytes += new FileInfo(finalPath).Length;
                    }
                    else
                    {
                        errors.Add($"Failed to pull {file}: {pullResult}");
                    }

                    File.Delete(localTempPath);
                }
            }

            return new SaveTransferResult
            {
                Success = transferredFiles.Any(),
                Direction = TransferDirection.AndroidToPc,
                FilesTransferred = transferredFiles.Count,
                BytesTransferred = totalBytes,
                TransferredFiles = transferredFiles,
                Errors = errors
            };
        }
        finally
        {
            // Clean up
            if (tempDirRef != null)
            {
                await ExecuteAdbCommandAsync($"shell rm -rf {tempDirRef}");
            }
        }
    }

    private async Task<string> ExecuteAdbCommandAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return "Failed to start ADB process";

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : $"{output}\n{error}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}