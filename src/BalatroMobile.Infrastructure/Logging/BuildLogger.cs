using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BalatroMobile.Infrastructure.Logging;

/// <summary>
/// Comprehensive logger for BalatroMobile build process.
/// Captures all information needed to diagnose issues.
/// </summary>
public class BuildLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly StringBuilder _buffer;
    private readonly object _lock = new();
    private bool _disposed;

    public string LogFilePath => _logFilePath;

    public BuildLogger()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BalatroMobile",
            "logs");
        
        Directory.CreateDirectory(logDir);
        
        // Create timestamped log file
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logFilePath = Path.Combine(logDir, $"build_{timestamp}.log");
        _buffer = new StringBuilder();

        // Write header
        WriteHeader();
    }

    private void WriteHeader()
    {
        Log("=".PadRight(80, '='));
        Log("BalatroMobile Build Log");
        Log($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log("=".PadRight(80, '='));
        Log("");
    }

    public void LogSystemInfo()
    {
        LogSection("SYSTEM INFORMATION");
        
        Log($"OS: {RuntimeInformation.OSDescription}");
        Log($"OS Architecture: {RuntimeInformation.OSArchitecture}");
        Log($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        Log($".NET Version: {RuntimeInformation.FrameworkDescription}");
        Log($"Machine Name: {Environment.MachineName}");
        Log($"User Name: {Environment.UserName}");
        Log($"Current Directory: {Environment.CurrentDirectory}");
        Log($"Temp Path: {Path.GetTempPath()}");
        Log($"LocalAppData: {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
        
        // Memory info
        var process = Process.GetCurrentProcess();
        Log($"Working Set: {process.WorkingSet64 / 1024 / 1024} MB");
        
        Log("");
    }

    public void LogToolPaths(string javaPath, string apkToolPath, string uberSignerPath, string love2dPath, string toolsDir)
    {
        LogSection("TOOL PATHS");
        
        Log($"Tools Directory: {toolsDir}");
        Log($"  Exists: {Directory.Exists(toolsDir)}");
        Log("");
        
        LogToolFile("Java", javaPath);
        LogToolFile("APKTool", apkToolPath);
        LogToolFile("uber-apk-signer", uberSignerPath);
        LogToolFile("Love2D APK", love2dPath);
        
        Log("");
    }

    private void LogToolFile(string name, string path)
    {
        var exists = File.Exists(path);
        var size = exists ? new FileInfo(path).Length : 0;
        
        Log($"{name}:");
        Log($"  Path: {path}");
        Log($"  Exists: {exists}");
        if (exists)
        {
            Log($"  Size: {size:N0} bytes ({size / 1024.0 / 1024.0:F2} MB)");
            Log($"  Modified: {File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}");
        }
    }

    public void LogToolDownload(string toolName, string url, bool success, string? error = null)
    {
        Log($"[DOWNLOAD] {toolName}");
        Log($"  URL: {url}");
        Log($"  Success: {success}");
        if (!success && error != null)
        {
            Log($"  Error: {error}");
        }
    }

    public void LogToolValidation(string toolName, bool exists, bool canRun, string? output = null, string? error = null)
    {
        Log($"[VALIDATE] {toolName}");
        Log($"  File Exists: {exists}");
        Log($"  Can Run: {canRun}");
        if (output != null)
        {
            Log($"  Output: {TruncateForLog(output)}");
        }
        if (error != null)
        {
            Log($"  Error: {TruncateForLog(error)}");
        }
    }

    public void LogJavaTest(string javaPath, bool success, int? exitCode, string? stdout, string? stderr)
    {
        LogSection("JAVA TEST");
        
        Log($"Java Path: {javaPath}");
        Log($"Success: {success}");
        Log($"Exit Code: {exitCode?.ToString() ?? "N/A"}");
        
        if (!string.IsNullOrEmpty(stdout))
        {
            Log($"STDOUT:");
            foreach (var line in stdout.Split('\n').Take(10))
            {
                Log($"  {line.Trim()}");
            }
        }
        
        if (!string.IsNullOrEmpty(stderr))
        {
            Log($"STDERR:");
            foreach (var line in stderr.Split('\n').Take(10))
            {
                Log($"  {line.Trim()}");
            }
        }
        
        Log("");
    }

    public void LogApkToolTest(string jarPath, string javaPath, bool success, string? output, string? error)
    {
        LogSection("APKTOOL TEST");
        
        Log($"APKTool JAR: {jarPath}");
        Log($"Using Java: {javaPath}");
        Log($"Success: {success}");
        
        if (!string.IsNullOrEmpty(output))
        {
            Log($"Output: {TruncateForLog(output)}");
        }
        
        if (!string.IsNullOrEmpty(error))
        {
            Log($"Error: {TruncateForLog(error)}");
        }
        
        Log("");
    }

    public void LogBalatroDetection(string? path, bool found)
    {
        LogSection("BALATRO DETECTION");
        
        Log($"Found: {found}");
        Log($"Path: {path ?? "N/A"}");
        
        if (found && path != null)
        {
            var exePath = Path.Combine(path, "Balatro.exe");
            Log($"Balatro.exe exists: {File.Exists(exePath)}");
            
            // Check for mods folder
            var modsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Balatro", "Mods");
            Log($"Mods folder: {modsPath}");
            Log($"Mods folder exists: {Directory.Exists(modsPath)}");
            
            if (Directory.Exists(modsPath))
            {
                var lovelyDumpPath = Path.Combine(modsPath, "lovely", "dump");
                Log($"Lovely dump exists: {Directory.Exists(lovelyDumpPath)}");
                
                if (Directory.Exists(lovelyDumpPath))
                {
                    var dumpFiles = Directory.GetFiles(lovelyDumpPath, "*.lua", SearchOption.AllDirectories);
                    Log($"Lovely dump files: {dumpFiles.Length}");
                }
            }
        }
        
        Log("");
    }

    public void LogPreFlightCheck(string checkName, string status, string? details = null)
    {
        Log($"[CHECK] {checkName}: {status}");
        if (details != null)
        {
            Log($"        {details}");
        }
    }

    public void LogBuildConfig(
        string platform,
        string fpsCap,
        bool landscape,
        bool highDpi,
        bool disableCrt,
        bool injectMods,
        string outputPath)
    {
        LogSection("BUILD CONFIGURATION");
        
        Log($"Platform: {platform}");
        Log($"FPS Cap: {fpsCap}");
        Log($"Landscape: {landscape}");
        Log($"High DPI: {highDpi}");
        Log($"Disable CRT: {disableCrt}");
        Log($"Inject Mods: {injectMods}");
        Log($"Output Path: {outputPath}");
        
        Log("");
    }

    public void LogBuildStep(string step, bool success, string? details = null)
    {
        var status = success ? "OK" : "FAILED";
        Log($"[BUILD] {step}: {status}");
        if (details != null)
        {
            Log($"        {details}");
        }
    }

    public void LogException(string context, Exception ex)
    {
        LogSection($"EXCEPTION in {context}");
        
        Log($"Type: {ex.GetType().FullName}");
        Log($"Message: {ex.Message}");
        Log($"Stack Trace:");
        foreach (var line in (ex.StackTrace ?? "").Split('\n'))
        {
            Log($"  {line.Trim()}");
        }
        
        if (ex.InnerException != null)
        {
            Log($"Inner Exception: {ex.InnerException.GetType().FullName}");
            Log($"Inner Message: {ex.InnerException.Message}");
        }
        
        Log("");
    }

    public void LogError(string message)
    {
        Log($"[ERROR] {message}");
    }

    public void LogWarning(string message)
    {
        Log($"[WARN] {message}");
    }

    public void LogInfo(string message)
    {
        Log($"[INFO] {message}");
    }

    public void LogDebug(string message)
    {
        Log($"[DEBUG] {message}");
    }

    public void LogSection(string title)
    {
        Log("");
        Log($"--- {title} ---");
        Log("");
    }

    public void LogEnvironmentVariables()
    {
        LogSection("RELEVANT ENVIRONMENT VARIABLES");
        
        var relevantVars = new[] { "PATH", "JAVA_HOME", "STEAM_GAME_PATH_BALATRO", "TEMP", "TMP" };
        
        foreach (var varName in relevantVars)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrEmpty(value))
            {
                // Truncate PATH since it can be very long
                if (varName == "PATH" && value.Length > 500)
                {
                    Log($"{varName}: {value.Substring(0, 500)}...[truncated]");
                }
                else
                {
                    Log($"{varName}: {value}");
                }
            }
            else
            {
                Log($"{varName}: (not set)");
            }
        }
        
        Log("");
    }

    public void LogDirectoryContents(string path, string label)
    {
        LogSection($"DIRECTORY CONTENTS: {label}");
        
        if (!Directory.Exists(path))
        {
            Log($"Directory does not exist: {path}");
            return;
        }
        
        Log($"Path: {path}");
        
        try
        {
            var files = Directory.GetFiles(path);
            var dirs = Directory.GetDirectories(path);
            
            Log($"Subdirectories ({dirs.Length}):");
            foreach (var dir in dirs.Take(20))
            {
                Log($"  [DIR] {Path.GetFileName(dir)}");
            }
            if (dirs.Length > 20)
            {
                Log($"  ... and {dirs.Length - 20} more");
            }
            
            Log($"Files ({files.Length}):");
            foreach (var file in files.Take(20))
            {
                var info = new FileInfo(file);
                Log($"  {Path.GetFileName(file)} ({info.Length:N0} bytes)");
            }
            if (files.Length > 20)
            {
                Log($"  ... and {files.Length - 20} more");
            }
        }
        catch (Exception ex)
        {
            Log($"Error reading directory: {ex.Message}");
        }
        
        Log("");
    }

    public void LogFinalResult(bool success, TimeSpan duration)
    {
        LogSection("FINAL RESULT");
        
        Log($"Success: {success}");
        Log($"Duration: {duration.TotalSeconds:F1} seconds");
        Log($"Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log("");
        Log($"Log file saved to: {_logFilePath}");
        
        Log("");
        Log("=".PadRight(80, '='));
        Log("END OF LOG");
        Log("=".PadRight(80, '='));
    }

    private void Log(string message)
    {
        lock (_lock)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            _buffer.AppendLine(line);
            
            // Write to file periodically
            if (_buffer.Length > 4096)
            {
                Flush();
            }
        }
    }

    private string TruncateForLog(string text, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(text))
            return "(empty)";
        
        text = text.Replace("\r", "").Replace("\n", " ").Trim();
        
        if (text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength) + "...[truncated]";
    }

    public void Flush()
    {
        lock (_lock)
        {
            if (_buffer.Length > 0)
            {
                try
                {
                    File.AppendAllText(_logFilePath, _buffer.ToString());
                    _buffer.Clear();
                }
                catch
                {
                    // Ignore write errors
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Flush();
            _disposed = true;
        }
    }
}
