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
            Console.WriteLine($"[APKTool] ERROR: APK not found: {apkPath}");
            return false;
        }

        Console.WriteLine($"[APKTool] Decompiling: {apkPath}");
        Console.WriteLine($"[APKTool] Output: {outputPath}");
        
        // Use -f (force) to overwrite, -s to skip sources (keep dex intact)
        // This matches the working script behavior
        var arguments = $"-Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true -jar \"{_apkToolJarPath}\" d -f -s -o \"{outputPath}\" \"{apkPath}\"";
        Console.WriteLine($"[APKTool] Command: java {arguments}");
        
        var result = await _javaTool.ExecuteAsync(arguments);
        
        // Verify decompilation worked
        if (result && Directory.Exists(outputPath))
        {
            var assetsPath = Path.Combine(outputPath, "assets");
            var manifestPath = Path.Combine(outputPath, "AndroidManifest.xml");
            Console.WriteLine($"[APKTool] Decompile result: {(Directory.Exists(outputPath) ? "OK" : "FAILED")}");
            Console.WriteLine($"[APKTool] Assets folder exists: {Directory.Exists(assetsPath)}");
            Console.WriteLine($"[APKTool] AndroidManifest.xml exists: {File.Exists(manifestPath)}");
        }
        
        return result;
    }

    public async Task<bool> CompileAsync(string inputPath, string outputPath)
    {
        if (!Directory.Exists(inputPath))
        {
            Console.WriteLine($"[APKTool] ERROR: Input directory not found: {inputPath}");
            return false;
        }

        // Log what's in the assets folder before compile
        var assetsPath = Path.Combine(inputPath, "assets");
        Console.WriteLine($"[APKTool] Compiling: {inputPath}");
        Console.WriteLine($"[APKTool] Output: {outputPath}");
        if (Directory.Exists(assetsPath))
        {
            var assetFiles = Directory.GetFiles(assetsPath);
            Console.WriteLine($"[APKTool] Assets folder contents ({assetFiles.Length} files):");
            foreach (var f in assetFiles)
            {
                var info = new FileInfo(f);
                Console.WriteLine($"[APKTool]   - {Path.GetFileName(f)}: {info.Length / 1024.0 / 1024.0:F2} MB");
            }
        }
        else
        {
            Console.WriteLine($"[APKTool] WARNING: No assets folder found!");
        }

        var arguments = $"-Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true -jar \"{_apkToolJarPath}\" b -o \"{outputPath}\" \"{inputPath}\"";
        Console.WriteLine($"[APKTool] Command: java {arguments}");
        
        var result = await _javaTool.ExecuteAsync(arguments);
        
        // Verify compilation worked
        if (File.Exists(outputPath))
        {
            var apkInfo = new FileInfo(outputPath);
            Console.WriteLine($"[APKTool] Compile SUCCESS: {outputPath} ({apkInfo.Length / 1024.0 / 1024.0:F2} MB)");
        }
        else
        {
            Console.WriteLine($"[APKTool] Compile FAILED: Output file not created");
        }
        
        return result && File.Exists(outputPath);
    }

    public async Task<bool> SignAsync(string apkPath)
    {
        Console.WriteLine($"[APKTool] Signing APK: {apkPath}");
        
        if (string.IsNullOrEmpty(_uberApkSignerPath) || !File.Exists(_uberApkSignerPath))
        {
            Console.WriteLine($"[APKTool] WARNING: uber-apk-signer not found, APK will be unsigned");
            return true;
        }

        if (!File.Exists(apkPath))
        {
            Console.WriteLine($"[APKTool] ERROR: APK to sign not found: {apkPath}");
            return false;
        }

        var apkInfo = new FileInfo(apkPath);
        Console.WriteLine($"[APKTool] Input APK size: {apkInfo.Length / 1024.0 / 1024.0:F2} MB");

        var arguments = $"-jar \"{_uberApkSignerPath}\" -a \"{apkPath}\"";
        Console.WriteLine($"[APKTool] Command: java {arguments}");
        
        var result = await _javaTool.ExecuteAsync(arguments);
        
        // uber-apk-signer creates: {filename}-aligned-debugSigned.apk
        var dir = Path.GetDirectoryName(apkPath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(apkPath);
        var signedPath = Path.Combine(dir, $"{baseName}-aligned-debugSigned.apk");
        
        if (File.Exists(signedPath))
        {
            var signedInfo = new FileInfo(signedPath);
            Console.WriteLine($"[APKTool] Sign SUCCESS: {signedPath} ({signedInfo.Length / 1024.0 / 1024.0:F2} MB)");
        }
        else
        {
            Console.WriteLine($"[APKTool] Sign WARNING: Expected signed APK not found at: {signedPath}");
            // List files in directory to help debug
            Console.WriteLine($"[APKTool] Files in {dir}:");
            foreach (var f in Directory.GetFiles(dir, "*.apk"))
            {
                Console.WriteLine($"[APKTool]   - {Path.GetFileName(f)}");
            }
        }
        
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
