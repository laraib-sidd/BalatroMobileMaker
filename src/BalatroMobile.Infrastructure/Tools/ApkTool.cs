using System.Diagnostics;

namespace BalatroMobile.Infrastructure.Tools;

public class ApkTool : IApkTool
{
    private readonly IJavaTool _javaTool;
    private readonly string _apkToolJarPath;
    private readonly string? _uberApkSignerPath;

    // #region agent log
    private static readonly string _debugLogPath = Path.Combine(Environment.CurrentDirectory, "debug.log");
    private static void DebugLog(string hypothesisId, string message, object? data = null)
    {
        try
        {
            var entry = System.Text.Json.JsonSerializer.Serialize(new { hypothesisId, location = "ApkTool.cs", message, data, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sessionId = "debug-session" });
            File.AppendAllText(_debugLogPath, entry + "\n");
        }
        catch { }
    }
    // #endregion

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
            // #region agent log
            DebugLog("A", "DecompileAsync APK not found", new { apkPath });
            // #endregion
            return false;
        }

        // Match original tool's JVM arguments exactly
        var arguments = $"-Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true -jar \"{_apkToolJarPath}\" d -s -o \"{outputPath}\" \"{apkPath}\"";
        
        // #region agent log
        DebugLog("A", "DecompileAsync executing", new { arguments, apkPath, outputPath });
        // #endregion
        
        var result = await _javaTool.ExecuteAsync(arguments);
        
        // #region agent log
        DebugLog("A", "DecompileAsync result", new { result, outputExists = Directory.Exists(outputPath) });
        // #endregion
        
        return result;
    }

    public async Task<bool> CompileAsync(string inputPath, string outputPath)
    {
        if (!Directory.Exists(inputPath))
        {
            // #region agent log
            DebugLog("A", "CompileAsync input dir not found", new { inputPath });
            // #endregion
            return false;
        }

        // Match original tool's JVM arguments exactly
        var arguments = $"-Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true -jar \"{_apkToolJarPath}\" b -o \"{outputPath}\" \"{inputPath}\"";
        
        // #region agent log
        DebugLog("A", "CompileAsync executing", new { arguments, inputPath, outputPath });
        // #endregion
        
        var result = await _javaTool.ExecuteAsync(arguments);
        
        // #region agent log
        DebugLog("A", "CompileAsync result", new { result, outputExists = File.Exists(outputPath) });
        // #endregion
        
        return result;
    }

    public async Task<bool> SignAsync(string apkPath)
    {
        if (string.IsNullOrEmpty(_uberApkSignerPath) || !File.Exists(_uberApkSignerPath))
        {
            // #region agent log
            DebugLog("B", "SignAsync signer not available", new { _uberApkSignerPath });
            // #endregion
            // No signer available - APK will be unsigned (can still be installed with adb)
            return true;
        }

        if (!File.Exists(apkPath))
        {
            // #region agent log
            DebugLog("B", "SignAsync APK not found", new { apkPath });
            // #endregion
            return false;
        }

        // Match original tool: -jar uber-apk-signer.jar -a balatro.apk
        var arguments = $"-jar \"{_uberApkSignerPath}\" -a \"{apkPath}\"";
        
        // #region agent log
        DebugLog("B", "SignAsync executing", new { arguments, apkPath });
        // #endregion
        
        var result = await _javaTool.ExecuteAsync(arguments);
        
        // #region agent log
        DebugLog("B", "SignAsync result", new { result });
        // #endregion
        
        return result;
    }

    public async Task<bool> IsAvailableAsync()
    {
        // Check if the jar file exists
        if (!File.Exists(_apkToolJarPath))
        {
            return false;
        }

        // Try to get version to verify it works
        var testArgs = $"-jar \"{_apkToolJarPath}\" --version";
        var result = await _javaTool.ExecuteAndCaptureOutputAsync(testArgs);

        // APKTool is available if we got a version number in the output
        // Version output looks like "2.9.3" or similar
        return !string.IsNullOrEmpty(result) && 
               !result.Contains("[Exception]") &&
               !result.Contains("Error") &&
               !result.Contains("not found");
    }

    public string GetVersion()
    {
        // Would need to parse version from apktool --version output
        return "Unknown";
    }
}