using System.Reactive;
using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private string? _gamePath;
    public string? GamePath { get => _gamePath; set => this.RaiseAndSetIfChanged(ref _gamePath, value); }

    private int _selectedFpsIndex;
    public int SelectedFpsIndex { get => _selectedFpsIndex; set => this.RaiseAndSetIfChanged(ref _selectedFpsIndex, value); }

    public string[] FpsOptions { get; } = ["Default", "30", "60"];

    private bool _enableLandscape = true;
    public bool EnableLandscape { get => _enableLandscape; set => this.RaiseAndSetIfChanged(ref _enableLandscape, value); }

    private bool _enableHighDpi;
    public bool EnableHighDpi { get => _enableHighDpi; set => this.RaiseAndSetIfChanged(ref _enableHighDpi, value); }

    private bool _disableCrtShader;
    public bool DisableCrtShader { get => _disableCrtShader; set => this.RaiseAndSetIfChanged(ref _disableCrtShader, value); }

    public string ToolsCachePath { get; }

    public ReactiveCommand<Unit, Unit> ResetDefaultsCommand { get; }

    public SettingsViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "settings";

        ToolsCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BalatroMobile", "tools");

        ResetDefaultsCommand = ReactiveCommand.Create(() =>
        {
            SelectedFpsIndex = 0;
            EnableLandscape = true;
            EnableHighDpi = false;
            DisableCrtShader = false;
        });

        DetectGamePath();
    }

    private async void DetectGamePath()
    {
        var detector = new BalatroMobile.Core.Services.GameDetection.GameDetector();
        GamePath = await detector.GetGameInstallPathAsync() ?? "Not detected — use Browse to locate";
    }
}
