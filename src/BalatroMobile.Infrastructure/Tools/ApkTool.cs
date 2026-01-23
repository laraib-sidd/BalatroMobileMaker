using System.Diagnostics;
using System.Text.Json;

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
        // #region agent log
        var debugLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BalatroMobile", "debug.log");
        Directory.CreateDirectory(Path.GetDirectoryName(debugLogPath)!);
        void Log(string hyp, string msg, object? data = null) { try { File.AppendAllText(debugLogPath, System.Text.Json.JsonSerializer.Serialize(new { hypothesisId = hyp, location = "ApkTool.cs:IsAvailableAsync", message = msg, data, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { } }
        // #endregion

        // #region agent log
        Log("C", "ApkTool jar path check", new { jarPath = _apkToolJarPath, exists = File.Exists(_apkToolJarPath) });
        // #endregion

        if (!File.Exists(_apkToolJarPath))
        {
            // #region agent log
            Log("C", "ApkTool jar NOT FOUND - returning false");
            // #endregion
            return false;
        }

        var testArgs = $"-jar \"{_apkToolJarPath}\" --version";
        // #region agent log
        Log("C", "Running ApkTool version check", new { testArgs });
        // #endregion

        var result = await _javaTool.ExecuteAndCaptureOutputAsync(testArgs);

        // #region agent log
        Log("C", "ApkTool version check result", new { resultLength = result?.Length ?? 0, resultPreview = result?.Substring(0, Math.Min(300, result?.Length ?? 0)), containsError = result?.Contains("Error") ?? false });
        // #endregion

        var isAvailable = !string.IsNullOrEmpty(result) && !result.Contains("Error");

        // #region agent log
        Log("C", "ApkTool availability decision", new { isAvailable });
        // #endregion

        return isAvailable;
    }

    public string GetVersion()
    {
        // Would need to parse version from apktool --version output
        return "Unknown";
    }
}