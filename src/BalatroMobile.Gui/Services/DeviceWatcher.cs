using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace BalatroMobile.Gui.Services;

public enum DeviceConnectionStatus
{
    NoBinary,
    Disconnected,
    Connected
}

public record DeviceState(DeviceConnectionStatus Status, string? DeviceName = null);

public static class DeviceWatcher
{
    public static IObservable<DeviceState> Watch(TimeSpan interval)
    {
        return Observable.Create<DeviceState>(observer =>
        {
            var timer = Observable.Timer(TimeSpan.Zero, interval)
                .SelectMany(_ => Observable.FromAsync(CheckDeviceAsync))
                .DistinctUntilChanged()
                .Subscribe(observer);
            return new CompositeDisposable(timer);
        });
    }

    private static async Task<DeviceState> CheckDeviceAsync()
    {
        try
        {
            var adbName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "adb.exe" : "adb";
            var toolsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BalatroMobile", "tools", "platform-tools");
            var adbPath = Path.Combine(toolsDir, adbName);

            if (!File.Exists(adbPath))
            {
                var systemAdb = FindInPath(adbName);
                if (systemAdb == null)
                    return new DeviceState(DeviceConnectionStatus.NoBinary);
                adbPath = systemAdb;
            }

            var psi = new ProcessStartInfo(adbPath, "devices")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return new DeviceState(DeviceConnectionStatus.NoBinary);

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !l.StartsWith("List of") && l.Contains('\t'))
                .ToList();

            if (lines.Count == 0)
                return new DeviceState(DeviceConnectionStatus.Disconnected);

            var deviceLine = lines.First();
            var deviceName = deviceLine.Split('\t')[0].Trim();
            return new DeviceState(DeviceConnectionStatus.Connected, deviceName);
        }
        catch
        {
            return new DeviceState(DeviceConnectionStatus.NoBinary);
        }
    }

    private static string? FindInPath(string executable)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        return pathVar.Split(separator)
            .Select(dir => Path.Combine(dir, executable))
            .FirstOrDefault(File.Exists);
    }
}
