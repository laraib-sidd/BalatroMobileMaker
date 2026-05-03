using System.Diagnostics;
using System.Reactive;
using BalatroMobile.Core.Models;
using BalatroMobile.Core.Services;
using BalatroMobile.Core.Services.GameDetection;
using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class SaveTransferViewModel : ViewModelBase
{
    private bool _pcToAndroid = true;
    public bool PcToAndroid
    {
        get => _pcToAndroid;
        set
        {
            this.RaiseAndSetIfChanged(ref _pcToAndroid, value);
            this.RaisePropertyChanged(nameof(DirectionLabel));
        }
    }

    public string DirectionLabel => PcToAndroid ? "PC → Android" : "Android → PC";

    private bool _isTransferring;
    public bool IsTransferring { get => _isTransferring; set => this.RaiseAndSetIfChanged(ref _isTransferring, value); }

    private double _progress;
    public double Progress { get => _progress; set => this.RaiseAndSetIfChanged(ref _progress, value); }

    private string? _statusMessage;
    public string? StatusMessage { get => _statusMessage; set => this.RaiseAndSetIfChanged(ref _statusMessage, value); }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; set => this.RaiseAndSetIfChanged(ref _errorMessage, value); }

    private bool _success;
    public bool Success { get => _success; set => this.RaiseAndSetIfChanged(ref _success, value); }

    public ReactiveCommand<Unit, Unit> TransferCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDirectionCommand { get; }

    public SaveTransferViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "save-transfer";

        var canTransfer = this.WhenAnyValue(x => x.IsTransferring, t => !t);
        TransferCommand = ReactiveCommand.CreateFromTask(RunTransfer, canTransfer);
        TransferCommand.ThrownExceptions
            .Subscribe(ex => { ErrorMessage = ex.Message; Debug.WriteLine($"Transfer error: {ex}"); });
        ToggleDirectionCommand = ReactiveCommand.Create(() => { PcToAndroid = !PcToAndroid; });
    }

    private async Task RunTransfer()
    {
        IsTransferring = true;
        ErrorMessage = null;
        Success = false;
        Progress = 10;
        StatusMessage = "Preparing transfer...";

        try
        {
            var config = new SaveTransferConfig
            {
                Direction = PcToAndroid ? TransferDirection.PcToAndroid : TransferDirection.AndroidToPc,
                CreateBackup = true
            };

            var platformDetector = new PlatformDetector();
            var transferService = new SaveTransferService(platformDetector);

            Progress = 30;
            StatusMessage = "Validating environment...";
            var valid = await transferService.ValidateTransferEnvironmentAsync(config);
            if (!valid)
            {
                ErrorMessage = "Environment validation failed. Check device connection and game installation.";
                return;
            }

            Progress = 50;
            StatusMessage = "Transferring saves...";
            var result = await transferService.TransferSavesAsync(config);

            if (result.Success)
            {
                Progress = 100;
                StatusMessage = $"Transferred {result.FilesTransferred} files successfully.";
                Success = true;
            }
            else
            {
                ErrorMessage = string.Join("\n", result.Errors);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsTransferring = false;
        }
    }
}
