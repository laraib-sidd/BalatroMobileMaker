namespace BalatroMobile.Infrastructure.Tools;

public interface IApkTool
{
    Task<bool> DecompileAsync(string apkPath, string outputPath);
    Task<bool> CompileAsync(string inputPath, string outputPath);
    Task<bool> SignAsync(string apkPath);
    Task<bool> IsAvailableAsync();
    string GetVersion();
}