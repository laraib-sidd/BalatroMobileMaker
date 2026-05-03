using Avalonia.ReactiveUI;
using BalatroMobile.Gui.ViewModels;

namespace BalatroMobile.Gui.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
