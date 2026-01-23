using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace BalatroMobile.Infrastructure.Tools;

/// <summary>
/// Manages downloading and caching of required external tools.
/// Tools are downloaded on first use and cached in the user's local app data.
/// </summary>
public class ToolsManager
{
    private readonly string _toolsDirectory;
    private readonly HttpClient _httpClient;
    private readonly Action<string>? _progressCallback;

    // Tool versions - update these when new versions are needed
    private const string APKTOOL_VERSION = "2.9.3";
    private const string UBER_APK_SIGNER_VERSION = "1.3.0";
    private const string LOVE2D_VERSION = "11.5";
    private const string OPENJDK_VERSION = "21.0.5";
    private const string OPENJDK_BUILD = "11";

    // Download URLs - VERIFIED WORKING URLs
    // APKTool: Using Bitbucket (official source) since GitHub doesn't host the jar directly
    private static readonly string APKTOOL_URL = $"https://bitbucket.org/iBotPeaches/apktool/downloads/apktool_{APKTOOL_VERSION}.jar";
    
    // uber-apk-signer: GitHub user is patrickfav (NOT nickreid)
    private static readonly string UBER_APK_SIGNER_URL = $"https://github.com/patrickfav/uber-apk-signer/releases/download/v{UBER_APK_SIGNER_VERSION}/uber-apk-signer-{UBER_APK_SIGNER_VERSION}.jar";
    
    // Love2D: Tag is "11.5a" not "11.5"
    private static readonly string LOVE2D_APK_URL = $"https://github.com/love2d/love-android/releases/download/{LOVE2D_VERSION}a/love-{LOVE2D_VERSION}-android-embed.apk";
    
    // OpenJDK - using Adoptium (Eclipse Temurin) portable JRE builds
    // Format: OpenJDK21U-jre_x64_windows_hotspot_21.0.5_11.zip
    private static readonly string OPENJDK_URL = RuntimeInformation.OSArchitecture == Architecture.Arm64
        ? $"https://github.com/adoptium/temurin21-binaries/releases/download/jdk-{OPENJDK_VERSION}%2B{OPENJDK_BUILD}/OpenJDK21U-jre_aarch64_windows_hotspot_{OPENJDK_VERSION}_{OPENJDK_BUILD}.zip"
        : $"https://github.com/adoptium/temurin21-binaries/releases/download/jdk-{OPENJDK_VERSION}%2B{OPENJDK_BUILD}/OpenJDK21U-jre_x64_windows_hotspot_{OPENJDK_VERSION}_{OPENJDK_BUILD}.zip";

    public string ToolsDirectory => _toolsDirectory;
    public string JavaPath => Path.Combine(_toolsDirectory, "jdk", "bin", "java.exe");
    public string ApkToolPath => Path.Combine(_toolsDirectory, "apktool.jar");
    public string UberApkSignerPath => Path.Combine(_toolsDirectory, "uber-apk-signer.jar");
    public string Love2dApkPath => Path.Combine(_toolsDirectory, "love-android-embed.apk");

    public ToolsManager(Action<string>? progressCallback = null)
    {
        _toolsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BalatroMobile",
            "tools");
        
        Directory.CreateDirectory(_toolsDirectory);
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BalatroMobile");
        _progressCallback = progressCallback;
    }

    /// <summary>
    /// Ensures all required tools are available, downloading them if necessary.
    /// </summary>
    public async Task<bool> EnsureToolsAvailableAsync()
    {
        try
        {
            var tasks = new List<Task<bool>>
            {
                EnsureJavaAsync(),
                EnsureApkToolAsync(),
                EnsureUberApkSignerAsync(),
                EnsureLove2dApkAsync()
            };

            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            ReportProgress($"Error ensuring tools: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if all tools are already available (no download needed).
    /// </summary>
    public bool AreToolsAvailable()
    {
        return IsJavaAvailable() && 
               File.Exists(ApkToolPath) && 
               File.Exists(UberApkSignerPath) && 
               File.Exists(Love2dApkPath);
    }

    /// <summary>
    /// Gets the Java executable path - uses bundled JRE if available, otherwise system Java.
    /// </summary>
    public string GetJavaExecutablePath()
    {
        // Prefer bundled Java
        if (File.Exists(JavaPath))
        {
            return JavaPath;
        }

        // Fall back to system Java
        return "java";
    }

    /// <summary>
    /// Checks if Java is available (either bundled or system).
    /// </summary>
    public bool IsJavaAvailable()
    {
        // Check bundled Java first
        if (File.Exists(JavaPath))
        {
            return true;
        }

        // Check system Java
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> EnsureJavaAsync()
    {
        // If Java is already available (bundled or system), skip download
        if (IsJavaAvailable())
        {
            ReportProgress("Java runtime: Available");
            return true;
        }

        // Only download on Windows - other platforms should use system Java
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ReportProgress("Java not found. Please install OpenJDK manually.");
            return false;
        }

        ReportProgress("Downloading portable Java runtime...");
        
        var jdkZipPath = Path.Combine(_toolsDirectory, "openjdk.zip");
        var jdkExtractPath = Path.Combine(_toolsDirectory, "jdk-temp");
        var jdkFinalPath = Path.Combine(_toolsDirectory, "jdk");

        try
        {
            // Download
            await DownloadFileAsync(OPENJDK_URL, jdkZipPath);

            // Extract
            ReportProgress("Extracting Java runtime...");
            if (Directory.Exists(jdkExtractPath))
            {
                Directory.Delete(jdkExtractPath, true);
            }
            ZipFile.ExtractToDirectory(jdkZipPath, jdkExtractPath);

            // Find the extracted JDK folder (it has a versioned name)
            var extractedFolder = Directory.GetDirectories(jdkExtractPath).FirstOrDefault();
            if (extractedFolder == null)
            {
                ReportProgress("Failed to find extracted JDK folder");
                return false;
            }

            // Move to final location
            if (Directory.Exists(jdkFinalPath))
            {
                Directory.Delete(jdkFinalPath, true);
            }
            Directory.Move(extractedFolder, jdkFinalPath);

            // Cleanup
            File.Delete(jdkZipPath);
            Directory.Delete(jdkExtractPath, true);

            ReportProgress("Java runtime: Downloaded and ready");
            return File.Exists(JavaPath);
        }
        catch (Exception ex)
        {
            ReportProgress($"Failed to download Java: {ex.Message}");
            
            // Cleanup on failure
            try
            {
                if (File.Exists(jdkZipPath)) File.Delete(jdkZipPath);
                if (Directory.Exists(jdkExtractPath)) Directory.Delete(jdkExtractPath, true);
            }
            catch { }
            
            return false;
        }
    }

    private async Task<bool> EnsureApkToolAsync()
    {
        if (File.Exists(ApkToolPath))
        {
            ReportProgress("APKTool: Available");
            return true;
        }

        ReportProgress($"Downloading APKTool v{APKTOOL_VERSION}...");
        
        try
        {
            await DownloadFileAsync(APKTOOL_URL, ApkToolPath);
            ReportProgress("APKTool: Downloaded and ready");
            return true;
        }
        catch (Exception ex)
        {
            ReportProgress($"Failed to download APKTool: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> EnsureUberApkSignerAsync()
    {
        if (File.Exists(UberApkSignerPath))
        {
            ReportProgress("uber-apk-signer: Available");
            return true;
        }

        ReportProgress($"Downloading uber-apk-signer v{UBER_APK_SIGNER_VERSION}...");
        
        try
        {
            await DownloadFileAsync(UBER_APK_SIGNER_URL, UberApkSignerPath);
            ReportProgress("uber-apk-signer: Downloaded and ready");
            return true;
        }
        catch (Exception ex)
        {
            ReportProgress($"Failed to download uber-apk-signer: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> EnsureLove2dApkAsync()
    {
        if (File.Exists(Love2dApkPath))
        {
            ReportProgress("Love2D APK: Available");
            return true;
        }

        ReportProgress($"Downloading Love2D v{LOVE2D_VERSION} Android APK...");
        
        try
        {
            await DownloadFileAsync(LOVE2D_APK_URL, Love2dApkPath);
            ReportProgress("Love2D APK: Downloaded and ready");
            return true;
        }
        catch (Exception ex)
        {
            ReportProgress($"Failed to download Love2D APK: {ex.Message}");
            return false;
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;
        var lastReportedPercent = -1;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (int)((downloadedBytes * 100) / totalBytes);
                if (percent != lastReportedPercent && percent % 10 == 0)
                {
                    ReportProgress($"  Downloading: {percent}%");
                    lastReportedPercent = percent;
                }
            }
        }
    }

    private void ReportProgress(string message)
    {
        _progressCallback?.Invoke(message);
    }

    /// <summary>
    /// Clears all cached tools (forces re-download on next run).
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_toolsDirectory))
            {
                Directory.Delete(_toolsDirectory, true);
            }
            Directory.CreateDirectory(_toolsDirectory);
            ReportProgress("Tool cache cleared");
        }
        catch (Exception ex)
        {
            ReportProgress($"Failed to clear cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the total size of cached tools in bytes.
    /// </summary>
    public long GetCacheSize()
    {
        if (!Directory.Exists(_toolsDirectory))
        {
            return 0;
        }

        return Directory.GetFiles(_toolsDirectory, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
    }
}
