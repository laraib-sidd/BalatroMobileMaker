using System.Diagnostics;

namespace BalatroMobile.Infrastructure.Tools;

public class ApkTool : IApkTool
{
    private readonly IJavaTool _javaTool;
    private readonly string _apkToolJarPath;
    private readonly string? _uberApkSignerPath;

    public ApkTool(IJavaTool javaTool, string apkToolJarPath, string? uberApkSignerPath = null)
    {
        _javaTool = javaTool;
        _apkToolJarPath = apkToolJarPath;
        _uberApkSignerPath = uberApkSignerPath;
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
        if (string.IsNullOrEmpty(_uberApkSignerPath) || !File.Exists(_uberApkSignerPath))
        {
            // No signer available - APK will be unsigned (can still be installed with adb)
            return true;
        }

        if (!File.Exists(apkPath))
        {
            return false;
        }

        // uber-apk-signer signs in place and creates aligned APK
        // --apks <path> : the APK to sign
        // --overwrite : overwrite the original file
        var arguments = $"-jar \"{_uberApkSignerPath}\" --apks \"{apkPath}\" --overwrite";
        return await _javaTool.ExecuteAsync(arguments);
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