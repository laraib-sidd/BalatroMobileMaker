using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services;
using BalatroMobile.Core.Services.GameDetection;
using BalatroMobile.Gui.Controls;
using BalatroMobile.Gui.Services;
using BalatroMobile.Infrastructure.Tools;
using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class HomeViewModel : ViewModelBase, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private CardStatus _gameStatus = CardStatus.Unknown;
    public CardStatus GameStatus { get => _gameStatus; set => this.RaiseAndSetIfChanged(ref _gameStatus, value); }

    private string _gameValue = "Checking...";
    public string GameValue { get => _gameValue; set => this.RaiseAndSetIfChanged(ref _gameValue, value); }

    private string? _gamePath;
    public string? GamePath { get => _gamePath; set => this.RaiseAndSetIfChanged(ref _gamePath, value); }

    private CardStatus _deviceStatus = CardStatus.Unknown;
    public CardStatus DeviceStatus { get => _deviceStatus; set => this.RaiseAndSetIfChanged(ref _deviceStatus, value); }

    private string _deviceValue = "Checking...";
    public string DeviceValue { get => _deviceValue; set => this.RaiseAndSetIfChanged(ref _deviceValue, value); }

    private CardStatus _toolsStatus = CardStatus.Unknown;
    public CardStatus ToolsStatus { get => _toolsStatus; set => this.RaiseAndSetIfChanged(ref _toolsStatus, value); }

    private string _toolsValue = "Checking...";
    public string ToolsValue { get => _toolsValue; set => this.RaiseAndSetIfChanged(ref _toolsValue, value); }

    private bool _isBuilding;
    public bool IsBuilding { get => _isBuilding; set => this.RaiseAndSetIfChanged(ref _isBuilding, value); }

    private string _buildStep = "";
    public string BuildStep { get => _buildStep; set => this.RaiseAndSetIfChanged(ref _buildStep, value); }

    private double _buildProgress;
    public double BuildProgress { get => _buildProgress; set => this.RaiseAndSetIfChanged(ref _buildProgress, value); }

    private string? _buildDetail;
    public string? BuildDetail { get => _buildDetail; set => this.RaiseAndSetIfChanged(ref _buildDetail, value); }

    private string? _buildError;
    public string? BuildError { get => _buildError; set => this.RaiseAndSetIfChanged(ref _buildError, value); }

    private bool _buildSuccess;
    public bool BuildSuccess { get => _buildSuccess; set => this.RaiseAndSetIfChanged(ref _buildSuccess, value); }

    public ReactiveCommand<Unit, Unit> BuildAndInstallCommand { get; }

    public HomeViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "home";

        var canBuild = this.WhenAnyValue(
            x => x.GameStatus, x => x.DeviceStatus, x => x.ToolsStatus, x => x.IsBuilding,
            (game, device, tools, building) =>
                game == CardStatus.Pass && device == CardStatus.Pass &&
                tools == CardStatus.Pass && !building);

        BuildAndInstallCommand = ReactiveCommand.CreateFromTask(RunBuildAndInstall, canBuild);

        this.WhenActivated(disposables =>
        {
            DeviceWatcher.Watch(TimeSpan.FromSeconds(3))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(state =>
                {
                    DeviceStatus = state.Status switch
                    {
                        DeviceConnectionStatus.Connected => CardStatus.Pass,
                        DeviceConnectionStatus.Disconnected => CardStatus.Warning,
                        _ => CardStatus.Fail
                    };
                    DeviceValue = state.Status switch
                    {
                        DeviceConnectionStatus.Connected => state.DeviceName ?? "Connected",
                        DeviceConnectionStatus.Disconnected => "Connect your Android device via USB",
                        _ => "ADB not found"
                    };
                })
                .DisposeWith(disposables);

            Observable.StartAsync(RunInitialChecks)
                .Subscribe()
                .DisposeWith(disposables);
        });
    }

    private async Task RunInitialChecks()
    {
        var gameDetector = new GameDetector();
        var gamePath = await gameDetector.GetGameInstallPathAsync();
        if (gamePath != null)
        {
            GameStatus = CardStatus.Pass;
            GameValue = "Balatro found";
            GamePath = gamePath;
        }
        else
        {
            GameStatus = CardStatus.Warning;
            GameValue = "Not auto-detected";
        }

        var toolsManager = new ToolsManager();
        var toolsReady = await toolsManager.EnsureToolsAvailableAsync();
        ToolsStatus = toolsReady ? CardStatus.Pass : CardStatus.Warning;
        ToolsValue = toolsReady ? "All tools ready" : "Downloading tools...";
    }

    private async Task RunBuildAndInstall()
    {
        IsBuilding = true;
        BuildError = null;
        BuildSuccess = false;

        try
        {
            var toolsManager = new ToolsManager(msg =>
                BuildDetail = msg);

            BuildStep = "Preparing tools...";
            BuildProgress = 5;
            await toolsManager.EnsureToolsAvailableAsync();

            BuildStep = "Building APK...";
            BuildProgress = 15;

            var gameDetector = new GameDetector();
            var patchService = new PatchService();
            var javaTool = new JavaTool(toolsManager.GetJavaExecutablePath());
            var apkTool = new ApkTool(
                javaTool,
                toolsManager.ApkToolPath,
                toolsManager.UberApkSignerPath);

            var buildService = new BuildService(
                gameDetector, patchService, apkTool, javaTool,
                toolsManager.Love2dApkPath, toolsManager.BalatroApkPatchPath);

            var config = new BuildConfig
            {
                Platform = Platform.Android,
                FpsCap = FpsCap.Default,
                EnableLandscape = true,
                OutputPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "balatro.apk")
            };

            var progress = new Progress<string>(msg =>
            {
                BuildDetail = msg;
                if (msg.Contains("Extract")) BuildProgress = 25;
                else if (msg.Contains("Patch")) BuildProgress = 40;
                else if (msg.Contains("game.love")) BuildProgress = 55;
                else if (msg.Contains("APK") || msg.Contains("apk")) BuildProgress = 70;
                else if (msg.Contains("Sign")) BuildProgress = 80;
            });

            var result = await buildService.BuildAsync(config, progress);

            if (!result.Success)
            {
                BuildError = string.Join("\n", result.Errors);
                return;
            }

            BuildStep = "Installing APK...";
            BuildProgress = 85;
            var modService = new ModTransferService(
                msg => BuildDetail = msg,
                toolsManager.AdbPath);

            var installed = await modService.InstallApkAsync(result.OutputPath!);
            if (!installed)
            {
                BuildError = "Failed to install APK on device. Check USB connection.";
                return;
            }

            BuildStep = "Transferring mods...";
            BuildProgress = 90;
            var prepResult = await modService.PrepareModPackageAsync(
                Path.Combine(Path.GetTempPath(), "BalatroMobile-mods"));

            if (prepResult.Success && prepResult.OutputPath != null)
            {
                var transferResult = await modService.TransferToDeviceAsync(prepResult.OutputPath);
                if (!transferResult.Success)
                {
                    BuildError = "APK installed but mod transfer failed:\n" +
                        string.Join("\n", transferResult.Errors);
                    return;
                }
            }

            BuildStep = "Done!";
            BuildProgress = 100;
            BuildDetail = "Modded Balatro is ready on your device. Launch the app and enjoy!";
            BuildSuccess = true;
        }
        catch (Exception ex)
        {
            BuildError = ex.Message;
        }
        finally
        {
            IsBuilding = false;
        }
    }
}
