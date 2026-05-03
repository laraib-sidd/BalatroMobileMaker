using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BalatroMobile.Gui.ViewModels;
using BalatroMobile.Gui.Views;
using ReactiveUI;
using Splat;

namespace BalatroMobile.Gui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Locator.CurrentMutable.Register(() => new HomeView(), typeof(IViewFor<HomeViewModel>));
        Locator.CurrentMutable.Register(() => new ModManagerView(), typeof(IViewFor<ModManagerViewModel>));
        Locator.CurrentMutable.Register(() => new SaveTransferView(), typeof(IViewFor<SaveTransferViewModel>));
        Locator.CurrentMutable.Register(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
        Locator.CurrentMutable.Register(() => new PreFlightView(), typeof(IViewFor<PreFlightViewModel>));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
