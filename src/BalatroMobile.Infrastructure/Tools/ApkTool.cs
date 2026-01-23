using System.Diagnostics;

namespace BalatroMobile.Infrastructure.Tools;

public class ApkTool : IApkTool
{
    private readonly IJavaTool _javaTool;
    private readonly string _apkToolJarPath;

    public ApkTool(IJavaTool javaTool, string apkToolJarPath)
    {
        _javaTool = javaTool;
        _apkToolJarPath = apkToolJarPath;
    }

    public async Task<bool> DecompileAsync(string apkPath, string outputPath)
    {
        if (!File.Exists(apkPath))
        {
            return false;
        }

        var arguments = $"-jar \"{_apkToolJarPath}\" d -s -o \"{outputPath}\" \"{apkPath}\"";
        return await _javaTool.ExecuteAsync(arguments);
    }

    public async Task<bool> CompileAsync(string inputPath, string outputPath)
    {
        if (!Directory.Exists(inputPath))
        {
            return false;
        }

        var arguments = $"-jar \"{_apkToolJarPath}\" b -o \"{outputPath}\" \"{inputPath}\"";
        return await _javaTool.ExecuteAsync(arguments);
    }

    public async Task<bool> SignAsync(string apkPath)
    {
        // APK signing would typically use a separate tool like uber-apk-signer
        // For now, return true as a placeholder
        return await Task.FromResult(true);
    }

    public async Task<bool> IsAvailableAsync()
    {
        if (!File.Exists(_apkToolJarPath))
        {
            return false;
        }

        var testArgs = $"-jar \"{_apkToolJarPath}\" --version";
        var result = await _javaTool.ExecuteAndCaptureOutputAsync(testArgs);
        return !string.IsNullOrEmpty(result) && !result.Contains("Error");
    }

    public string GetVersion()
    {
        // Would need to parse version from apktool --version output
        return "Unknown";
    }
}