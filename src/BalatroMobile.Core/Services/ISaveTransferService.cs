using BalatroMobile.Core.Models;

namespace BalatroMobile.Core.Services;

public interface ISaveTransferService
{
    Task<SaveTransferResult> TransferSavesAsync(SaveTransferConfig config);
    Task<bool> ValidateTransferEnvironmentAsync(SaveTransferConfig config);
    Task<IEnumerable<string>> GetAvailableSaveFilesAsync();
    Task<string?> CreateBackupAsync(string sourcePath);
    Task<bool> RestoreBackupAsync(string backupPath, string targetPath);
}