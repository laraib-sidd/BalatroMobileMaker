using System.Diagnostics;
using System.Reactive;
using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class MainWindowViewModel : ReactiveObject, IScreen
{
    public RoutingState Router { get; } = new();

    public ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; }
    public ReactiveCommand<Unit, IRoutableViewModel> GoMods { get; }
    public ReactiveCommand<Unit, IRoutableViewModel> GoSaveTransfer { get; }
    public ReactiveCommand<Unit, IRoutableViewModel> GoSettings { get; }
    public ReactiveCommand<Unit, IRoutableViewModel> GoPreFlight { get; }

    public MainWindowViewModel()
    {
        GoHome = ReactiveCommand.CreateFromObservable(
            () => Router.Navigate.Execute(new HomeViewModel(this)));
        GoMods = ReactiveCommand.CreateFromObservable(
            () => Router.Navigate.Execute(new ModManagerViewModel(this)));
        GoSaveTransfer = ReactiveCommand.CreateFromObservable(
            () => Router.Navigate.Execute(new SaveTransferViewModel(this)));
        GoSettings = ReactiveCommand.CreateFromObservable(
            () => Router.Navigate.Execute(new SettingsViewModel(this)));
        GoPreFlight = ReactiveCommand.CreateFromObservable(
            () => Router.Navigate.Execute(new PreFlightViewModel(this)));

        GoHome.ThrownExceptions.Subscribe(ex => Debug.WriteLine($"Navigation error: {ex}"));
        GoMods.ThrownExceptions.Subscribe(ex => Debug.WriteLine($"Navigation error: {ex}"));
        GoSaveTransfer.ThrownExceptions.Subscribe(ex => Debug.WriteLine($"Navigation error: {ex}"));
        GoSettings.ThrownExceptions.Subscribe(ex => Debug.WriteLine($"Navigation error: {ex}"));
        GoPreFlight.ThrownExceptions.Subscribe(ex => Debug.WriteLine($"Navigation error: {ex}"));

        GoHome.Execute().Subscribe();
    }
}
