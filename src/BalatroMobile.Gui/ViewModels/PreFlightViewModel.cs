using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using BalatroMobile.Configuration.Models;
using BalatroMobile.Core.Services;
using BalatroMobile.Core.Services.GameDetection;
using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class CheckItem : ReactiveObject
{
    public string Name { get; }
    public string Description { get; }

    private CheckResult _result = CheckResult.Unknown;
    public CheckResult Result { get => _result; set => this.RaiseAndSetIfChanged(ref _result, value); }

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }

    private string? _fixSuggestion;
    public string? FixSuggestion { get => _fixSuggestion; set => this.RaiseAndSetIfChanged(ref _fixSuggestion, value); }

    private string _statusIcon = "⬜";
    public string StatusIcon { get => _statusIcon; set => this.RaiseAndSetIfChanged(ref _statusIcon, value); }

    public CheckItem(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public void UpdateIcon()
    {
        StatusIcon = Result switch
        {
            CheckResult.Pass => "✅",
            CheckResult.Fail => "❌",
            CheckResult.Warning => "⚠️",
            _ => IsRunning ? "⏳" : "⬜"
        };
    }
}

public class PreFlightViewModel : ViewModelBase, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
    public ObservableCollection<CheckItem> Checks { get; } = new();

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }

    private string _summary = "";
    public string Summary { get => _summary; set => this.RaiseAndSetIfChanged(ref _summary, value); }

    public ReactiveCommand<Unit, Unit> RunAllCommand { get; }

    public PreFlightViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "preflight";

        var canRun = this.WhenAnyValue(x => x.IsRunning, r => !r);
        RunAllCommand = ReactiveCommand.CreateFromTask(RunAllChecks, canRun);
        RunAllCommand.ThrownExceptions
            .Subscribe(ex => { Summary = $"Error: {ex.Message}"; Debug.WriteLine($"PreFlight error: {ex}"); });

        this.WhenActivated(disposables =>
        {
            Observable.StartAsync(RunAllChecks)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(
                    _ => { },
                    ex => Debug.WriteLine($"PreFlight auto-run error: {ex}"))
                .DisposeWith(disposables);
        });
    }

    private async Task RunAllChecks()
    {
        IsRunning = true;
        Checks.Clear();
        Summary = "Running checks...";

        try
        {
            var gameDetector = new GameDetector();
            var platformDetector = new PlatformDetector();
            var service = new PreFlightCheckService(gameDetector, platformDetector);

            var allChecks = await service.GetAllChecksAsync();
            foreach (var check in allChecks)
            {
                var item = new CheckItem(check.Name, check.Description)
                {
                    FixSuggestion = check.FixSuggestion,
                    IsRunning = true
                };
                item.UpdateIcon();
                Checks.Add(item);
            }

            var result = await service.RunAllChecksAsync();
            foreach (var detail in result.Results)
            {
                var item = Checks.FirstOrDefault(c => c.Name == detail.CheckName);
                if (item != null)
                {
                    item.Result = detail.Result;
                    item.IsRunning = false;
                    if (detail.FixSuggestion != null)
                        item.FixSuggestion = detail.FixSuggestion;
                    item.UpdateIcon();
                }
            }

            var passed = result.Results.Count(r => r.Result == CheckResult.Pass);
            var total = result.Results.Count();
            Summary = result.AllPassed
                ? $"All {total} checks passed!"
                : $"{passed} of {total} checks passed";
        }
        catch (Exception ex)
        {
            Summary = $"Error running checks: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
