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

        // Match original tool's JVM arguments exactly
        var arguments = $"-Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true -jar \"{_apkToolJarPath}\" d -s -o \"{outputPath}\" \"{apkPath}\"";
        return await _javaTool.ExecuteAsync(arguments);
    }

    public async Task<bool> CompileAsync(string inputPath, string outputPath)
    {
        if (!Directory.Exists(inputPath))
        {
            return false;
        }

        // Match original tool's JVM arguments exactly
        var arguments = $"-Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true -jar \"{_apkToolJarPath}\" b -o \"{outputPath}\" \"{inputPath}\"";
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

        // Match original tool: -jar uber-apk-signer.jar -a balatro.apk
        var arguments = $"-jar \"{_uberApkSignerPath}\" -a \"{apkPath}\"";
        return await _javaTool.ExecuteAsync(arguments);
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
